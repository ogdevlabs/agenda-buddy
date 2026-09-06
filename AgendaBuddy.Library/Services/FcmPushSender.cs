using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgendaBuddy.Library.Services;

/// <summary>
/// Sends through Firebase Cloud Messaging's HTTP v1 API. One POST per message, no SDK.
/// </summary>
/// <remarks>
/// <para>
/// HTTP v1 rather than the legacy <c>Authorization: key=…</c> endpoint, because that endpoint was shut down in
/// 2024. v1 wants a short-lived OAuth2 access token, and the only way to mint one without the Google SDK is to
/// sign a JWT with the service account's private key and exchange it — which is what
/// <see cref="GetAccessTokenAsync"/> does. Hand-rolled with <see cref="RSA"/> from the BCL rather than by
/// adding a JWT package to this project, which has none and needs none for 30 lines.
/// </para>
/// <para>
/// Registered only when a project id and service-account credential are both present; without them
/// <see cref="UnconfiguredPushSender"/> is registered instead. So this class never has to answer "what if it is
/// not configured" — reaching it means it is.
/// </para>
/// <para>
/// <b>Every message states its priority, its sound and its Android channel, because FCM's defaults are wrong
/// for this product in three ways that all present as "push does not work".</b> A v1 message defaults to
/// <i>normal</i> priority, which Android may hold until the device leaves Doze — minutes to hours, and an
/// appointment request delivered tomorrow morning is not a notification; <c>apns-priority: 10</c> is the iOS
/// equivalent, and is valid here precisely because every message carries a visible alert (a background-only
/// push would have to use 5). A notification with no sound is drawn silently, which is indistinguishable from
/// not arriving at all unless the screen happens to be watched. And Android 8+ posts on a channel, so a message
/// naming none lands on the one the Firebase SDK auto-creates — labelled "Miscellaneous", at an importance
/// nothing here chose, leaving the app's own channel settings inert.
/// </para>
/// <para>
/// Like <see cref="ResendEmailSender"/>, it never throws on a delivery failure and never logs the message body
/// or the device token: the body names an appointment and the token identifies a device, which is what
/// <c>PiiRedactingProcessor</c> exists to keep out of exported telemetry.
/// </para>
/// </remarks>
public class FcmPushSender : IPushSender
{
    public const string HttpClientName = "Fcm";

    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string DefaultTokenUri = "https://oauth2.googleapis.com/token";

    /// <summary>Renew this long before expiry, so a token cannot lapse mid-flight.</summary>
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(2);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FcmPushSender>? _logger;
    private readonly PushOptions _options;
    private readonly ServiceAccount _serviceAccount;

    // One token is shared by every send. The semaphore means a burst of notifications mints one token rather
    // than one each, which also matters because Google rate-limits the exchange.
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public FcmPushSender(
        IHttpClientFactory httpClientFactory,
        IOptions<PushOptions> options,
        ILogger<FcmPushSender>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
        _serviceAccount = ParseServiceAccount(_options.ServiceAccountJson!);
    }

    public async Task<bool> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken)) return false;

        try
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken);
            if (accessToken is null) return false;

            var client = _httpClientFactory.CreateClient(HttpClientName);

            var payload = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new { title, body },
                    // Every value must be a string -- FCM rejects a data payload with non-string values.
                    data = data?.ToDictionary(pair => pair.Key, pair => pair.Value),
                    // Per-platform delivery instructions. FCM's defaults are wrong for this product in two ways
                    // that both look like "push does not work" -- see the remarks below on priority and sound.
                    // The field names are FCM's own snake_case, which the camelCase policy leaves untouched
                    // because their first character is already lowercase.
                    android = new
                    {
                        priority = "high",
                        notification = new
                        {
                            channel_id = PushOptions.AndroidChannelId,
                            default_sound = true
                        }
                    },
                    apns = new
                    {
                        headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
                        payload = new { aps = new { sound = "default" } }
                    }
                }
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_options.FirebaseProjectId}/messages:send")
            {
                Content = JsonContent.Create(payload, options: SendJsonOptions)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", accessToken);

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode) return true;

            // 404 is FCM's answer for a token it no longer recognises -- the app was uninstalled, or the token
            // rotated. Logged distinctly because it means the stored token is stale, not that push is broken.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger?.LogInformation(
                    "push.token-unregistered: FCM no longer recognises this device token; it should be re-registered");
                return false;
            }

            // Status only. An FCM error body echoes the message, which carries appointment detail.
            _logger?.LogWarning("push.send-failed: FCM answered {StatusCode}", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            // Swallowed by contract (see IPushSender): a delivery failure must not fail the appointment that
            // triggered it.
            _logger?.LogWarning(ex, "push.send-failed: could not reach FCM");
            return false;
        }
    }

    /// <summary>
    /// A cached OAuth2 access token, minted by signing a JWT with the service account's private key and
    /// exchanging it at Google's token endpoint.
    /// </summary>
    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt - RenewBefore)
            return _accessToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Re-checked inside the lock: everything queued behind the first caller wants the token it minted,
            // not one each.
            if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt - RenewBefore)
                return _accessToken;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = BuildSignedAssertion()
            });

            var response = await client.PostAsync(_serviceAccount.TokenUri, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Status only: the response body to a failed token exchange can echo the assertion.
                _logger?.LogWarning(
                    "push.token-exchange-failed: Google answered {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (token is null || string.IsNullOrEmpty(token.AccessToken)) return null;

            _accessToken = token.AccessToken;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            return _accessToken;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "push.token-exchange-failed: could not reach Google's token endpoint");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// The RS256 JWT Google's token endpoint accepts in exchange for an access token.
    /// </summary>
    private string BuildSignedAssertion()
    {
        var issuedAt = DateTimeOffset.UtcNow;

        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = _serviceAccount.ClientEmail,
            ["scope"] = Scope,
            ["aud"] = _serviceAccount.TokenUri,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            // One hour is Google's maximum for an assertion.
            ["exp"] = issuedAt.AddHours(1).ToUnixTimeSeconds()
        }));

        var signingInput = $"{header}.{claims}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_serviceAccount.PrivateKey);
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    /// <summary>Base64url, per RFC 7515: URL-safe alphabet, no padding.</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// The three fields of the service-account JSON this needs.
    /// </summary>
    /// <remarks>
    /// Parsed once at construction and deliberately allowed to throw: a malformed credential is a
    /// misconfiguration to fix, not a delivery failure to swallow, and failing at startup is where it is
    /// cheapest to notice. <see cref="AddPushDelivery"/>-time validation is what keeps this from being reached
    /// with nothing configured at all.
    /// </remarks>
    private static ServiceAccount ParseServiceAccount(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var clientEmail = root.TryGetProperty("client_email", out var email) ? email.GetString() : null;
        var privateKey = root.TryGetProperty("private_key", out var key) ? key.GetString() : null;
        var tokenUri = root.TryGetProperty("token_uri", out var uri) ? uri.GetString() : null;

        if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException(
                $"{PushOptions.Section}:{nameof(PushOptions.ServiceAccountJson)} is not a Firebase "
                + "service-account key: it must contain 'client_email' and 'private_key'. Download it from "
                + "Firebase Console → Project settings → Service accounts → Generate new private key.");
        }

        return new ServiceAccount(
            clientEmail,
            privateKey,
            string.IsNullOrWhiteSpace(tokenUri) ? DefaultTokenUri : tokenUri);
    }

    // Omits nulls so a notification with no appointment sends no `data` key at all, rather than `data: null`,
    // which FCM rejects.
    private static readonly JsonSerializerOptions SendJsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };


    private sealed record ServiceAccount(string ClientEmail, string PrivateKey, string TokenUri);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
