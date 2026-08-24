using System.Net.Http.Json;
using System.Text.Json;
using MobileApp.Infrastructure;
using MobileApp.Models;
using MobileApp.Routing;

namespace MobileApp.Services;

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

    public Task LogoutAsync()
    {
        _secureStorage.Remove(JwtDelegatingHandler.JwtKey);
        _secureStorage.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync()
    {
        return _secureStorage.GetAsync(JwtDelegatingHandler.JwtKey);
    }
}
