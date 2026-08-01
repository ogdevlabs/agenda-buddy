using System.Net;
using System.Net.Http;
using System.Text.Json;
using MobileApp.Infrastructure;
using MobileApp.Services;
using Moq;
using Xunit;

namespace MobileApp.Tests.Services;

public class PushNotificationServiceTests
{
    [Fact]
    public async Task RegisterTokenAsync_Success_PostsToIdentityService()
    {
        var handler = new TestableHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(httpClient);

        var storage = new Mock<ISecureStorageService>();

        var sut = new PushNotificationService(factory.Object, storage.Object);

        await sut.PostTokenAsync("fcm-token-xyz", "android");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("device-token", handler.LastRequest.RequestUri!.ToString().Replace("https://localhost/", ""));

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("fcm-token-xyz", doc.RootElement.GetProperty("token").GetString());
        Assert.Equal("android", doc.RootElement.GetProperty("platform").GetString());
    }

    [Fact]
    public async Task PostTokenAsync_HttpFailure_DoesNotThrow()
    {
        var handler = new TestableHttpMessageHandler(HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(httpClient);

        var storage = new Mock<ISecureStorageService>();

        var sut = new PushNotificationService(factory.Object, storage.Object);

        var ex = await Record.ExceptionAsync(() => sut.PostTokenAsync("tok", "ios"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task PostTokenAsync_HandlerThrows_DoesNotPropagate()
    {
        var handler = new ThrowingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(httpClient);

        var storage = new Mock<ISecureStorageService>();

        var sut = new PushNotificationService(factory.Object, storage.Object);

        var ex = await Record.ExceptionAsync(() => sut.PostTokenAsync("tok", "ios"));
        Assert.Null(ex);
    }

    private sealed class TestableHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public TestableHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }
}
