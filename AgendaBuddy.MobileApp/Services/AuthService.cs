using System.Net.Http.Json;
using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Routing;

namespace AgendaBuddy.MobileApp.Services;

public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecureStorageService _secureStorage;
    private readonly PushNotificationService? _pushNotificationService;

    internal const string RefreshTokenKey = "refresh_token";

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ISecureStorageService secureStorage,
        PushNotificationService? pushNotificationService = null)
    {
        _httpClientFactory = httpClientFactory;
        _secureStorage = secureStorage;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<bool> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");

        var route = AuthRouteBuilder.Login();
        var payload = new { email, password };
        var response = await client.PostAsJsonAsync(route.Path, payload, ct);

        if (!response.IsSuccessStatusCode)
            return false;

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: ct);

        if (loginResponse is null || string.IsNullOrEmpty(loginResponse.AccessToken))
            return false;

        await _secureStorage.SetAsync(JwtDelegatingHandler.JwtKey, loginResponse.AccessToken);
        await _secureStorage.SetAsync(RefreshTokenKey, loginResponse.RefreshToken);

        if (_pushNotificationService is not null)
            await _pushNotificationService.InitializeAsync();

        return true;
    }

    public async Task<bool> RegisterAsync(string email, string password, string role, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");

        var route = AuthRouteBuilder.Register();
        var payload = new { email, password, role };
        var response = await client.PostAsJsonAsync(route.Path, payload, ct);

        if (!response.IsSuccessStatusCode)
            return false;

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: ct);

        if (loginResponse is null || string.IsNullOrEmpty(loginResponse.AccessToken))
            return false;

        await _secureStorage.SetAsync(JwtDelegatingHandler.JwtKey, loginResponse.AccessToken);
        await _secureStorage.SetAsync(RefreshTokenKey, loginResponse.RefreshToken);

        if (_pushNotificationService is not null)
            await _pushNotificationService.InitializeAsync();

        return true;
    }

    /// <summary>
    /// Calls the server-side logout endpoint (invalidating the refresh token,
    /// per Identity's single-use semantics) in addition to clearing local storage. Both must
    /// happen. The local clear runs in <c>finally</c> so a user tapping logout always ends up
    /// logged out on this device, even when the server call fails — but that failure is not
    /// swallowed: like <see cref="LoginAsync"/> and <see cref="RegisterAsync"/> above, this method
    /// does not catch a network exception, so it propagates to the caller after the clear.
    /// </summary>
    /// <remarks>
    /// It also gives up this device's push registration, and that call has to come <b>first</b>: the route
    /// authorises off the JWT this method is about to delete, and there is no other way to say which account is
    /// releasing the device. Without it the signed-out account stayed addressable — every notification for it,
    /// subject and body included, kept arriving on a device it no longer controlled.
    /// </remarks>
    public async Task LogoutAsync()
    {
        // Before anything clears the JWT, and outside the try below so a push-unregistration failure cannot be
        // mistaken for a logout failure. UnregisterTokenAsync absorbs its own errors by contract.
        if (_pushNotificationService is not null)
            await _pushNotificationService.UnregisterTokenAsync();

        try
        {
            var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                // Read before the storage clear below, and sent through the no-auth client (not
                // JwtDelegatingHandler) deliberately — this call must never trigger that
                // handler's own 401-refresh-and-retry, which would rotate the refresh token this
                // request is trying to invalidate out from under it.
                var accessToken = await _secureStorage.GetAsync(JwtDelegatingHandler.JwtKey);
                var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");
                var route = AuthRouteBuilder.Logout();
                await client.PostAsJsonAsync(route.Path, new { refreshToken, accessToken });
            }
        }
        finally
        {
            _secureStorage.Remove(JwtDelegatingHandler.JwtKey);
            _secureStorage.Remove(RefreshTokenKey);
        }
    }

    public Task<string?> GetTokenAsync()
    {
        return _secureStorage.GetAsync(JwtDelegatingHandler.JwtKey);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Push is unregistered <b>first</b>, before the credential goes, for the reason <see cref="LogoutAsync"/>
    /// does it in that order: the route that releases the registration authorises off the token being deleted, so
    /// afterwards there is no way to reach it, and a device left registered keeps receiving push — subject and
    /// body included — for an account that no longer exists.
    /// </para>
    /// <para>
    /// Sent through the <b>authenticated</b> client, unlike <see cref="LogoutAsync"/>: there is no refresh token
    /// in the request for a transparent 401-retry to rotate out from under it, and the route needs the caller's
    /// claim to know whose account to delete.
    /// </para>
    /// <para>
    /// The local session is cleared in a <c>finally</c>, so a failed or unreachable delete still signs the device
    /// out. Leaving somebody logged in to an account they have just asked to be deleted is the worse failure, and
    /// the caller reports the outcome from the return value rather than from what is still on the device.
    /// </para>
    /// </remarks>
    public async Task<bool> DeleteAccountAsync(CancellationToken ct = default)
    {
        if (_pushNotificationService is not null)
            await _pushNotificationService.UnregisterTokenAsync();

        try
        {
            var client = _httpClientFactory.CreateClient("AgendaBuddyApi");
            var route = AuthRouteBuilder.DeleteAccount();
            var response = await client.DeleteAsync(route.Path, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _secureStorage.Remove(JwtDelegatingHandler.JwtKey);
            _secureStorage.Remove(RefreshTokenKey);
        }
    }

    public async Task<bool> RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");
        var route = AuthRouteBuilder.RequestPasswordReset();
        var response = await client.PostAsJsonAsync(route.Path, new { email }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ConfirmPasswordResetAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");
        var route = AuthRouteBuilder.ConfirmPasswordReset();
        var response = await client.PostAsJsonAsync(route.Path, new { email, token, newPassword }, ct);
        return response.IsSuccessStatusCode;
    }
}
