using System.Net;
using System.Net.Http;
using System.Text;
using MobileApp.Infrastructure;
using MobileApp.Services;
using Moq;
using Xunit;

namespace MobileApp.Tests.Services;

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

        var factory = CreateFactory(HttpStatusCode.OK, """{"token":"abc123"}""");
        var sut = new AuthService(factory, storage.Object);

        var result = await sut.LoginAsync("user@example.com", "password123");

        Assert.True(result);
        storage.Verify(s => s.SetAsync(JwtDelegatingHandler.JwtKey, "abc123"), Times.Once);
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

    [Fact]
    public async Task LogoutAsync_ClearsSecureStorage()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        // Pre-populate so there is a token to clear
        var factory = CreateFactory(HttpStatusCode.OK, """{"token":"tok-xyz"}""");
        var sut = new AuthService(factory, storage.Object);
        await sut.LoginAsync("u@test.com", "p");

        await sut.LogoutAsync();

        storage.Verify(s => s.Remove(JwtDelegatingHandler.JwtKey), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Minimal fake handler — avoids a dependency on MockHttp or similar packages
    // ---------------------------------------------------------------------------
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly HttpContent? _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, HttpContent? content = null)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = _content ?? new StringContent(string.Empty)
            };
            return Task.FromResult(response);
        }
    }
}
