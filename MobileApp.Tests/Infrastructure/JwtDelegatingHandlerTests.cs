using System.Net;
using System.Net.Http;
using System.Text;
using MobileApp.Infrastructure;
using Moq;
using Xunit;

namespace MobileApp.Tests.Infrastructure;

public class JwtDelegatingHandlerTests
{
    // Builds a JwtDelegatingHandler wired up with a FakeInnerHandler so we can
    // intercept the outbound request and control the response status code.
    private static (JwtDelegatingHandler handler, FakeInnerHandler inner) BuildSut(
        ISecureStorageService storage, IHttpClientFactory? httpClientFactory = null)
    {
        var inner = new FakeInnerHandler(HttpStatusCode.OK);
        var handler = new JwtDelegatingHandler(storage, httpClientFactory ?? Mock.Of<IHttpClientFactory>())
        {
            InnerHandler = inner
        };
        return (handler, inner);
    }

    private static IHttpClientFactory RefreshFactory(HttpStatusCode statusCode, string? jsonContent = null)
    {
        var content = jsonContent is not null
            ? new StringContent(jsonContent, Encoding.UTF8, "application/json")
            : new StringContent(string.Empty);

        var refreshHandler = new FakeInnerHandler(statusCode, content);
        var client = new HttpClient(refreshHandler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApiNoAuth")).Returns(client);
        return factory.Object;
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

        Assert.NotNull(inner.Requests.Single().Headers.Authorization);
        Assert.Equal("Bearer", inner.Requests.Single().Headers.Authorization!.Scheme);
        Assert.Equal("token123", inner.Requests.Single().Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task SendAsync_On401_RefreshFails_ClearsStorageAndRaisesEvent()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey))
               .ReturnsAsync("expired-token");
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.RefreshTokenKey))
               .ReturnsAsync("expired-refresh-token");

        var inner = new FakeInnerHandler(HttpStatusCode.Unauthorized);
        var refreshFactory = RefreshFactory(HttpStatusCode.Unauthorized); // refresh itself rejected
        var handler = new JwtDelegatingHandler(storage.Object, refreshFactory) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        EventHandler onUnauthorized = (_, _) => { };
        bool eventFired = false;
        onUnauthorized = (_, _) => eventFired = true;
        JwtDelegatingHandler.UnauthorizedAccess += onUnauthorized;

        try
        {
            await invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/protected"),
                CancellationToken.None);
        }
        finally
        {
            JwtDelegatingHandler.UnauthorizedAccess -= onUnauthorized;
        }

        storage.Verify(s => s.Remove(JwtDelegatingHandler.JwtKey), Times.Once);
        storage.Verify(s => s.Remove(JwtDelegatingHandler.RefreshTokenKey), Times.Once);
        Assert.True(eventFired);
        // Only the one original attempt reached the protected resource — no retry against a
        // rejected refresh.
        Assert.Single(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_On401_NoStoredRefreshToken_ClearsStorageAndRaisesEvent()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey))
               .ReturnsAsync("expired-token");
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.RefreshTokenKey))
               .ReturnsAsync((string?)null);

        var inner = new FakeInnerHandler(HttpStatusCode.Unauthorized);
        var handler = new JwtDelegatingHandler(storage.Object, Mock.Of<IHttpClientFactory>()) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        EventHandler onUnauthorized = (_, _) => { };
        bool eventFired = false;
        onUnauthorized = (_, _) => eventFired = true;
        JwtDelegatingHandler.UnauthorizedAccess += onUnauthorized;

        try
        {
            await invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/protected"),
                CancellationToken.None);
        }
        finally
        {
            JwtDelegatingHandler.UnauthorizedAccess -= onUnauthorized;
        }

        storage.Verify(s => s.Remove(JwtDelegatingHandler.JwtKey), Times.Once);
        Assert.True(eventFired);
    }

    // ---------------------------------------------------------------------------
    // AC9 — transparent refresh-on-401
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_On401_RefreshSucceeds_RetriesOriginalRequestWithNewTokenAndNoEvent()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey))
               .ReturnsAsync("expired-token");
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.RefreshTokenKey))
               .ReturnsAsync("valid-refresh-token");
        storage.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(Task.CompletedTask);

        // First send: 401. Second send (the retry): 200.
        var inner = new SequencedInnerHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        var refreshFactory = RefreshFactory(
            HttpStatusCode.OK, """{"accessToken":"new-access-token","refreshToken":"new-refresh-token"}""");

        var handler = new JwtDelegatingHandler(storage.Object, refreshFactory) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        bool eventFired = false;
        EventHandler onUnauthorized = (_, _) => eventFired = true;
        JwtDelegatingHandler.UnauthorizedAccess += onUnauthorized;

        HttpResponseMessage response;
        try
        {
            response = await invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/protected"),
                CancellationToken.None);
        }
        finally
        {
            JwtDelegatingHandler.UnauthorizedAccess -= onUnauthorized;
        }

        // The caller sees the retried response, not the original 401.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(eventFired);

        // Two attempts against the protected resource: the original 401, then the retry.
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal("expired-token", inner.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal("new-access-token", inner.Requests[1].Headers.Authorization!.Parameter);
        Assert.Equal(inner.Requests[0].RequestUri, inner.Requests[1].RequestUri);
        Assert.Equal(inner.Requests[0].Method, inner.Requests[1].Method);

        // New tokens were persisted.
        storage.Verify(s => s.SetAsync(JwtDelegatingHandler.JwtKey, "new-access-token"), Times.Once);
        storage.Verify(s => s.SetAsync(JwtDelegatingHandler.RefreshTokenKey, "new-refresh-token"), Times.Once);

        // No purge — the session survived.
        storage.Verify(s => s.Remove(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_On401_RefreshSucceeds_RetriesPostWithOriginalBody()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey))
               .ReturnsAsync("expired-token");
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.RefreshTokenKey))
               .ReturnsAsync("valid-refresh-token");
        storage.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>()))
               .Returns(Task.CompletedTask);

        var inner = new SequencedInnerHandler(HttpStatusCode.Unauthorized, HttpStatusCode.Created);
        var refreshFactory = RefreshFactory(
            HttpStatusCode.OK, """{"accessToken":"new-access-token","refreshToken":"new-refresh-token"}""");

        var handler = new JwtDelegatingHandler(storage.Object, refreshFactory) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/booking/appointments/1/notes")
        {
            Content = new StringContent("""{"content":"session note"}""", Encoding.UTF8, "application/json")
        };

        var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);

        var retried = inner.Requests[1];
        var retriedBody = await retried.Content!.ReadAsStringAsync();
        Assert.Equal("""{"content":"session note"}""", retriedBody);
        Assert.Equal("new-access-token", retried.Headers.Authorization!.Parameter);
    }

    // ---------------------------------------------------------------------------
    // AC10 — never auto-retry an ambiguous write
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_PostTimesOut_ThrowsAmbiguousWriteException_AndDoesNotRetry()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey)).ReturnsAsync("token123");

        // Simulates HttpClient's own configured Timeout firing: a TaskCanceledException whose
        // CancellationToken is NOT the one the caller passed in.
        var inner = new TimeoutInnerHandler();
        var handler = new JwtDelegatingHandler(storage.Object, Mock.Of<IHttpClientFactory>()) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/booking/appointments")
        {
            Content = new StringContent("""{"customerEmail":"c@test.com"}""", Encoding.UTF8, "application/json")
        };

        await Assert.ThrowsAsync<AmbiguousWriteException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        // Exactly one attempt was made — the client never auto-resubmitted the write.
        Assert.Equal(1, inner.SendCount);
    }

    [Fact]
    public async Task SendAsync_PutTimesOut_ThrowsAmbiguousWriteException()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey)).ReturnsAsync("token123");

        var inner = new TimeoutInnerHandler();
        var handler = new JwtDelegatingHandler(storage.Object, Mock.Of<IHttpClientFactory>()) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Put, "https://localhost/api/v1/booking/1")
        {
            Content = new StringContent("""{"status":"Confirmed"}""", Encoding.UTF8, "application/json")
        };

        await Assert.ThrowsAsync<AmbiguousWriteException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        Assert.Equal(1, inner.SendCount);
    }

    [Fact]
    public async Task SendAsync_GetTimesOut_PropagatesTaskCanceledException_NotWrapped()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey)).ReturnsAsync("token123");

        var inner = new TimeoutInnerHandler();
        var handler = new JwtDelegatingHandler(storage.Object, Mock.Of<IHttpClientFactory>()) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        // A GET is safe to retry by nature — AC10 only guards non-idempotent writes — so a timeout
        // here should surface as the ordinary TaskCanceledException, not the ambiguous-write type.
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test"),
                CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_PostReturns502FromGateway_ThrowsAmbiguousWriteException()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey)).ReturnsAsync("token123");

        var inner = new FakeInnerHandler(HttpStatusCode.BadGateway);
        var handler = new JwtDelegatingHandler(storage.Object, Mock.Of<IHttpClientFactory>()) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/booking/appointments/1/payment")
        {
            Content = new StringContent("""{"amount":50}""", Encoding.UTF8, "application/json")
        };

        await Assert.ThrowsAsync<AmbiguousWriteException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        Assert.Single(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_PostReturns504FromGateway_ThrowsAmbiguousWriteException()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey)).ReturnsAsync("token123");

        var inner = new FakeInnerHandler(HttpStatusCode.GatewayTimeout);
        var handler = new JwtDelegatingHandler(storage.Object, Mock.Of<IHttpClientFactory>()) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/messaging/threads/1/messages")
        {
            Content = new StringContent("""{"body":"hi"}""", Encoding.UTF8, "application/json")
        };

        await Assert.ThrowsAsync<AmbiguousWriteException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        Assert.Single(inner.Requests);
    }

    [Fact]
    public async Task SendAsync_GetReturns502FromGateway_DoesNotThrow()
    {
        var storage = new Mock<ISecureStorageService>();
        storage.Setup(s => s.GetAsync(JwtDelegatingHandler.JwtKey)).ReturnsAsync("token123");

        var inner = new FakeInnerHandler(HttpStatusCode.BadGateway);
        var handler = new JwtDelegatingHandler(storage.Object, Mock.Of<IHttpClientFactory>()) { InnerHandler = inner };
        var invoker = new HttpMessageInvoker(handler);

        // Reads are not the concern of AC10 — a 502 on a GET is just an ordinary failed response,
        // returned to the caller like any other non-success status.
        var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    private sealed class FakeInnerHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly HttpContent? _content;
        public List<HttpRequestMessage> Requests { get; } = new();

        public FakeInnerHandler(HttpStatusCode statusCode, HttpContent? content = null)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = new HttpResponseMessage(_statusCode);
            if (_content is not null)
                response.Content = _content;
            return Task.FromResult(response);
        }
    }

    private sealed class SequencedInnerHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statusCodes;
        public List<HttpRequestMessage> Requests { get; } = new();

        public SequencedInnerHandler(params HttpStatusCode[] statusCodes)
        {
            _statusCodes = new Queue<HttpStatusCode>(statusCodes);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var code = _statusCodes.Count > 0 ? _statusCodes.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(code));
        }
    }

    private sealed class TimeoutInnerHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            // Mirrors what HttpClient itself throws when its configured Timeout elapses: a
            // TaskCanceledException carrying a *different* (already-cancelled) token than the one
            // the caller passed in — never the caller's own cancellationToken.
            throw new TaskCanceledException("The request timed out.", null, new CancellationToken(true));
        }
    }
}
