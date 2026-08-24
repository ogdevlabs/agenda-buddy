using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MobileApp.Models;
using MobileApp.Routing;

namespace MobileApp.Infrastructure;

public class JwtDelegatingHandler : DelegatingHandler
{
    public const string JwtKey = "jwt";

    // Duplicated from Services.AuthService.RefreshTokenKey by design: Infrastructure must not
    // depend on Services (the dependency runs the other way), and the two constants are the same
    // secure-storage literal, not a coincidence to be refactored away.
    internal const string RefreshTokenKey = "refresh_token";

    // AC10: the gateway-hop failure modes that leave a write's outcome ambiguous — the backend may
    // have already processed it. 502/504 are the two the task calls out explicitly; a request
    // timeout is handled separately below via TaskCanceledException.
    private static readonly HttpStatusCode[] AmbiguousGatewayStatusCodes =
    {
        HttpStatusCode.BadGateway,
        HttpStatusCode.GatewayTimeout,
    };

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly ISecureStorageService _secureStorage;
    private readonly IHttpClientFactory _httpClientFactory;

    public JwtDelegatingHandler(ISecureStorageService secureStorage, IHttpClientFactory httpClientFactory)
    {
        _secureStorage = secureStorage;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _secureStorage.GetAsync(JwtKey);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // AC9: clone before the first send. HttpRequestMessage can only be sent once, and we may
        // need to resend this exact request — with a new access token — after a 401 triggers a
        // successful refresh below.
        var requestClone = await request.CloneAsync();

        var isNonIdempotentWrite = IsNonIdempotentWrite(request.Method);

        var response = await SendGuardingAmbiguousWrites(request, isNonIdempotentWrite, cancellationToken);

        if (isNonIdempotentWrite && AmbiguousGatewayStatusCodes.Contains(response.StatusCode))
        {
            throw new AmbiguousWriteException(
                $"The gateway returned {(int)response.StatusCode} for a {request.Method} request to " +
                $"{request.RequestUri}. The backend may have already processed this write — it was not " +
                "automatically retried.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var newAccessToken = await TryRefreshTokenAsync(cancellationToken);
            if (newAccessToken is not null)
            {
                response.Dispose();

                requestClone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);

                // Retry the original request exactly once, now that a valid access token exists.
                // This is unconditional on HTTP method: unlike the ambiguous-write case above, the
                // *reason* for this retry (a stale token) is known and has just been resolved, so
                // resending is safe regardless of whether the original call was a write.
                return await SendGuardingAmbiguousWrites(requestClone, isNonIdempotentWrite, cancellationToken);
            }

            // Refresh itself failed (network error, or the refresh token is invalid/expired/already
            // consumed per F-021's single-use semantics) — fall back to the existing reactive logout.
            _secureStorage.Remove(JwtKey);
            _secureStorage.Remove(RefreshTokenKey);
            UnauthorizedAccess?.Invoke(this, EventArgs.Empty);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendGuardingAmbiguousWrites(
        HttpRequestMessage request, bool isNonIdempotentWrite, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (isNonIdempotentWrite && !cancellationToken.IsCancellationRequested)
        {
            // The caller's own token was not signalled, so this is HttpClient's configured request
            // timeout firing, not the caller giving up. The write may already be sitting on the
            // backend. AC10: surface this distinctly rather than letting anything retry it.
            throw new AmbiguousWriteException(
                $"The {request.Method} request to {request.RequestUri} timed out. The backend may have " +
                "already processed this write — it was not automatically retried.", ex);
        }
    }

    /// <summary>
    /// Returns the new access token on success, so the caller can retry with it directly rather
    /// than reading storage back — a mock/fake caller has no way to make a second GetAsync call
    /// return something different from the first, and production code shouldn't need to rely on
    /// that round-trip either. Returns <c>null</c> on any failure (network error, malformed
    /// response, or a rejected/expired/already-consumed refresh token per F-021's single-use
    /// semantics) — the caller falls back to reactive logout in that case.
    /// </summary>
    private async Task<string?> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
        if (string.IsNullOrEmpty(refreshToken))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");
            var route = AuthRouteBuilder.Refresh();

            var response = await client.PostAsJsonAsync(route.Path, new { refreshToken }, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var refreshed = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
            if (refreshed is null || string.IsNullOrEmpty(refreshed.AccessToken))
                return null;

            await _secureStorage.SetAsync(JwtKey, refreshed.AccessToken);
            await _secureStorage.SetAsync(RefreshTokenKey, refreshed.RefreshToken);
            return refreshed.AccessToken;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Judgment call (AC10): POST is unconditionally a write. For PUT, this codebase has no
    // per-request metadata declaring idempotency (e.g. BookingApiService.UpdateStatusAsync PUTs a
    // status *transition*, which is not safe to replay). Absent that signal, every PUT is treated
    // as non-idempotent too — the conservative default the task asked for.
    private static bool IsNonIdempotentWrite(HttpMethod method) =>
        method == HttpMethod.Post || method == HttpMethod.Put;

    public static event EventHandler? UnauthorizedAccess;
}
