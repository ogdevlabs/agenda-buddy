using System.Net;
using System.Net.Http;
using MobileApp.Infrastructure;
using Moq;
using Xunit;

namespace MobileApp.Tests.Infrastructure;

public class JwtDelegatingHandlerTests
{
    // Builds a JwtDelegatingHandler wired up with a FakeInnerHandler so we can
    // intercept the outbound request and control the response status code.
    private static (JwtDelegatingHandler handler, FakeInnerHandler inner) BuildSut(
        ISecureStorageService storage)
    {
        var inner = new FakeInnerHandler(HttpStatusCode.OK);
        var handler = new JwtDelegatingHandler(storage)
        {
            InnerHandler = inner
        };
        return (handler, inner);
    }

    [Fact]
    public async Task SendAsync_WithToken_SetsAuthorizationHeader()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey))
               .ReturnsAsync("token123");

        var (handler, inner) = BuildSut(storage.Object);
        var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test"),
            CancellationToken.None);

        Assert.NotNull(inner.LastRequest?.Headers.Authorization);
        Assert.Equal("Bearer", inner.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("token123", inner.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_On401_ClearsStorageAndRaisesEvent()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey))
               .ReturnsAsync("expired-token");

        var inner = new FakeInnerHandler(HttpStatusCode.Unauthorized);
        var handler = new JwtDelegatingHandler(storage.Object) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        bool eventFired = false;
        JwtDelegatingHandler.UnauthorizedAccess += (_, _) => eventFired = true;

        try
        {
            await invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/protected"),
                CancellationToken.None);
        }
        finally
        {
            // Unsubscribe so static event doesn't bleed across tests
            JwtDelegatingHandler.UnauthorizedAccess -= (_, _) => eventFired = true;
        }

        storage.Verify(s => s.Remove(JwtDelegatingHandler.JwtKey), Times.Once);
        Assert.True(eventFired);
    }

    // -----------------------------------------------------------------------
    private sealed class FakeInnerHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeInnerHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
