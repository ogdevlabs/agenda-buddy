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
    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");
                var route = AuthRouteBuilder.Logout();
                await client.PostAsJsonAsync(route.Path, new { refreshToken });
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
}
