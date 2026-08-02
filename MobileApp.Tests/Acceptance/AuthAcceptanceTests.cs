using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MobileApp.Tests.Acceptance;

[Trait("Category", "Acceptance")]
public class AuthAcceptanceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;
    private readonly string _testEmail;

    public AuthAcceptanceTests()
    {
        var baseUrl = Environment.GetEnvironmentVariable("IDENTITY_BASE_URL") ?? "http://localhost:6036/";
        _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _testEmail = $"test-{Guid.NewGuid():N}@acceptance.test";
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _client.GetAsync("api/v1/auth/register", cts.Token);
            // Any response (even 405) means the service is alive
        }
        catch
        {
            Skip.If(true, "Identity service is not reachable at " + _client.BaseAddress);
        }
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task RegisterProvider_ReturnsTokens()
    {
        var payload = new { email = _testEmail, password = "SecurePass123!", role = "Provider" };

        var response = await _client.PostAsJsonAsync("api/v1/auth/register", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [SkippableFact]
    public async Task RegisterCustomer_ReturnsTokens()
    {
        var payload = new { email = _testEmail, password = "SecurePass123!", role = "Customer" };

        var response = await _client.PostAsJsonAsync("api/v1/auth/register", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [SkippableFact]
    public async Task Login_AfterRegistration_ReturnsTokens()
    {
        const string password = "SecurePass123!";
        var registerPayload = new { email = _testEmail, password, role = "Provider" };
        var registerResponse = await _client.PostAsJsonAsync("api/v1/auth/register", registerPayload);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginPayload = new { email = _testEmail, password };
        var loginResponse = await _client.PostAsJsonAsync("api/v1/auth/login", loginPayload);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [SkippableFact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var payload = new { email = "nonexistent@example.com", password = "WrongPass!" };

        var response = await _client.PostAsJsonAsync("api/v1/auth/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task RegisterProvider_DuplicateEmail_ReturnsConflict()
    {
        var payload = new { email = _testEmail, password = "SecurePass123!", role = "Provider" };
        var first = await _client.PostAsJsonAsync("api/v1/auth/register", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("api/v1/auth/register", payload);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [SkippableFact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var payload = new { email = "not-an-email", password = "SecurePass123!", role = "Provider" };

        var response = await _client.PostAsJsonAsync("api/v1/auth/register", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var payload = new { email = _testEmail, password = "short", role = "Provider" };

        var response = await _client.PostAsJsonAsync("api/v1/auth/register", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record TokenResponse(string AccessToken, string RefreshToken);
}
