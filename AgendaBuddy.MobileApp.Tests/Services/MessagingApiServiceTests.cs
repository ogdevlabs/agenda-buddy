using System.Net;
using System.Text;
using AgendaBuddy.MobileApp.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class MessagingApiServiceTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static IHttpClientFactory CreateFactory(HttpStatusCode statusCode, string? jsonContent = null)
    {
        var content = jsonContent is not null
            ? new StringContent(jsonContent, Encoding.UTF8, "application/json")
            : new StringContent(string.Empty);

        var handler = new FakeHttpMessageHandler(statusCode, content);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(client);
        return factory.Object;
    }

    // ---------------------------------------------------------------------------
    // GetInboxAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetInbox_Returns200_DeserializesThreadList()
    {
        var json = """
            [
                {"threadId":"t1","otherPartyEmail":"alice@example.com","lastMessageBody":"Hello!","lastMessageAt":"2026-07-31T09:00:00Z","unreadCount":2},
                {"threadId":"t2","otherPartyEmail":"bob@example.com","lastMessageBody":"See you soon","lastMessageAt":"2026-07-31T08:00:00Z","unreadCount":0}
            ]
            """;

        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetInboxAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("t1", result[0].ThreadId);
        Assert.Equal("alice@example.com", result[0].OtherPartyEmail);
        Assert.Equal(2, result[0].UnreadCount);
        Assert.Equal("t2", result[1].ThreadId);
    }

    [Fact]
    public async Task GetInbox_Returns401_ReturnsEmptyList()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.Unauthorized));

        var result = await sut.GetInboxAsync();

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // SendMessageAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SendMessage_Returns201_ReturnsSummary()
    {
        var json = """
            {"id":"m1","threadId":"t1","senderEmail":"provider@example.com","body":"Hi there!","sentAt":"2026-07-31T10:00:00Z","isRead":false}
            """;

        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.Created, json));

        var result = await sut.SendMessageAsync("alice@example.com", "Hi there!");

        Assert.NotNull(result);
        Assert.Equal("m1", result!.Id);
        Assert.Equal("t1", result.ThreadId);
        Assert.Equal("Hi there!", result.Body);
        Assert.False(result.IsRead);
    }

    [Fact]
    public async Task SendMessage_Returns400_ReturnsNull()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.BadRequest));

        var result = await sut.SendMessageAsync("bad-email", "body");

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // MarkReadAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MarkRead_Returns200_ReturnsUpdated()
    {
        var json = """
            {"id":"m1","threadId":"t1","senderEmail":"alice@example.com","body":"Hello!","sentAt":"2026-07-31T09:00:00Z","isRead":true}
            """;

        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.MarkReadAsync("m1");

        Assert.NotNull(result);
        Assert.Equal("m1", result!.Id);
        Assert.True(result.IsRead);
    }

    [Fact]
    public async Task MarkRead_Returns404_ReturnsNull()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.NotFound));

        var result = await sut.MarkReadAsync("nonexistent");

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Fake handler
    // ---------------------------------------------------------------------------

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly HttpContent _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, HttpContent content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = _content });
        }
    }
}
