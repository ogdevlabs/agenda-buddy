using System.Net.Http.Json;
using System.Text.Json;
using MobileApp.Infrastructure;
using MobileApp.Models;

namespace MobileApp.Services;

public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecureStorageService _secureStorage;

    public AuthService(IHttpClientFactory httpClientFactory, ISecureStorageService secureStorage)
    {
        _httpClientFactory = httpClientFactory;
        _secureStorage = secureStorage;
    }

    public async Task<bool> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("AgendaBuddyApiNoAuth");

        var payload = new { email, password };
        var response = await client.PostAsJsonAsync("identity/login", payload, ct);

        if (!response.IsSuccessStatusCode)
            return false;

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: ct);

        if (loginResponse is null || string.IsNullOrEmpty(loginResponse.Token))
            return false;

        await _secureStorage.SetAsync(JwtDelegatingHandler.JwtKey, loginResponse.Token);
        return true;
    }

    public Task LogoutAsync()
    {
        _secureStorage.Remove(JwtDelegatingHandler.JwtKey);
        return Task.CompletedTask;
    }

    public Task<string?> GetTokenAsync()
    {
        return _secureStorage.GetAsync(JwtDelegatingHandler.JwtKey);
    }
}
