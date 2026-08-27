using System.Net;
using System.Net.Http;
using System.Text;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class AuthServiceTests
{
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string? jsonContent = null)
    {
        var content = jsonContent is not null
            ? new StringContent(jsonContent, Encoding.UTF8, "application/json")
            : null;

        var handler = new FakeHttpMessageHandler(statusCode, content);
        return new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
    }

    private static IHttpClientFactory CreateFactory(HttpStatusCode statusCode, string? jsonContent = null)
    {
        var client = CreateHttpClient(statusCode, jsonContent);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApiNoAuth")).Returns(client);
        return factory.Object;
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_StoresTokenAndReturnsTrue()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var factory = CreateFactory(HttpStatusCode.OK, """{"accessToken":"abc123","refreshToken":"rt456"}""");
        var sut = new AuthService(factory, storage.Object);

        var result = await sut.LoginAsync("user@example.com", "password123");

        Assert.True(result);
        storage.Verify(s => s.SetAsync(JwtDelegatingHandler.JwtKey, "abc123"), Times.Once);
        storage.Verify(s => s.SetAsync(AuthService.RefreshTokenKey, "rt456"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsFalse()
    {
        var storage = new Mock<ISecureStorageService>();

        var factory = CreateFactory(HttpStatusCode.Unauthorized);
        var sut = new AuthService(factory, storage.Object);

        var result = await sut.LoginAsync("user@example.com", "wrongpassword");

        Assert.False(result);
        storage.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // F-015-T10 / AC11 — LogoutAsync calls the server-side logout endpoint (in addition to
    // clearing local storage), carrying the stored refresh token so Identity can invalidate it.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_WithStoredRefreshToken_PostsToLogoutRouteAndClearsStorage()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(AuthService.RefreshTokenKey)).ReturnsAsync("stored-refresh-token");

        var handler = new FakeHttpMessageHandler(HttpStatusCode.NoContent);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApiNoAuth")).Returns(client);

        var sut = new AuthService(factory.Object, storage.Object);

        await sut.LogoutAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("api/v1/auth/logout", request.RequestUri!.AbsolutePath.TrimStart('/'));

        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("stored-refresh-token", body);

        storage.Verify(s => s.Remove(JwtDelegatingHandler.JwtKey), Times.Once);
        storage.Verify(s => s.Remove(AuthService.RefreshTokenKey), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_NoStoredRefreshToken_ClearsStorageWithoutCallingServer()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(AuthService.RefreshTokenKey)).ReturnsAsync((string?)null);

        var handler = new FakeHttpMessageHandler(HttpStatusCode.NoContent);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApiNoAuth")).Returns(client);

        var sut = new AuthService(factory.Object, storage.Object);

        await sut.LogoutAsync();

        // Nothing to invalidate server-side, so no request is made — but the local clear still
        // happens unconditionally below.
        Assert.Empty(handler.Requests);
        storage.Verify(s => s.Remove(JwtDelegatingHandler.JwtKey), Times.Once);
        storage.Verify(s => s.Remove(AuthService.RefreshTokenKey), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ServerCallThrows_StillClearsStorage_AndPropagatesTheFailure()
    {
        // A user tapping logout must always end up logged out locally, even when the server is
        // unreachable — but this file's existing convention (LoginAsync/RegisterAsync never catch a
        // network exception) is followed here too: the failure is not swallowed, it propagates to
        // the caller after the local clear has happened.
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(AuthService.RefreshTokenKey)).ReturnsAsync("stored-refresh-token");

        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("network unreachable"));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApiNoAuth")).Returns(client);

        var sut = new AuthService(factory.Object, storage.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.LogoutAsync());

        storage.Verify(s => s.Remove(JwtDelegatingHandler.JwtKey), Times.Once);
        storage.Verify(s => s.Remove(AuthService.RefreshTokenKey), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Minimal fake handlers — avoids a dependency on MockHttp or similar packages
    // ---------------------------------------------------------------------------
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly HttpContent? _content;

        public List<HttpRequestMessage> Requests { get; } = new();

        public FakeHttpMessageHandler(HttpStatusCode statusCode, HttpContent? content = null)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = _content ?? new StringContent(string.Empty)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }
}
