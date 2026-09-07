using System.Net;
using System.Text;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
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

    private static IUserSessionService CreateSession(string email = "me@example.com")
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(email);
        return session.Object;
    }

    // ---------------------------------------------------------------------------
    // GetInboxAsync
    // ---------------------------------------------------------------------------

    // GetInbox (MessageModule.cs) answers a FLAT array of raw MessageEntity objects — one per message, not
    // one per thread — so GetInboxAsync has to group by threadId itself.
    [Fact]
    public async Task GetInbox_Returns200_GroupsFlatMessagesIntoThreads()
    {
        var json = """
            [
                {"id":"m1","threadId":"t1","senderEmail":"alice@example.com","recipientEmail":"me@example.com","body":"Hello!","sentAt":"2026-07-31T09:00:00Z","isRead":false},
                {"id":"m2","threadId":"t1","senderEmail":"me@example.com","recipientEmail":"alice@example.com","body":"Hi Alice!","sentAt":"2026-07-31T09:05:00Z","isRead":true},
                {"id":"m3","threadId":"t2","senderEmail":"bob@example.com","recipientEmail":"me@example.com","body":"See you soon","sentAt":"2026-07-31T08:00:00Z","isRead":true}
            ]
            """;

        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.OK, json), CreateSession());

        var result = await sut.GetInboxAsync();

        Assert.Equal(2, result.Count);
        // Ordered by most recent message first.
        Assert.Equal("t1", result[0].ThreadId);
        Assert.Equal("alice@example.com", result[0].OtherPartyEmail);
        Assert.Equal("Hi Alice!", result[0].LastMessageBody);
        // Only m1 (unread, addressed to me) counts — m2 was sent by me.
        Assert.Equal(1, result[0].UnreadCount);
        Assert.Equal("t2", result[1].ThreadId);
        Assert.Equal("bob@example.com", result[1].OtherPartyEmail);
        Assert.Equal(0, result[1].UnreadCount);
    }

    // An empty list must mean an empty inbox and nothing else. Answering one for a failed read is what let the
    // page draw "No messages yet" over a conversation that exists, with its error banner unreachable.
    [Fact]
    public async Task GetInbox_Returns401_Throws()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.Unauthorized), CreateSession());

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetInboxAsync());
    }

    [Fact]
    public async Task GetInbox_Returns500_Throws()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.InternalServerError), CreateSession());

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetInboxAsync());
    }

    [Fact]
    public async Task GetThread_Returns500_Throws()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.InternalServerError), CreateSession());

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetThreadAsync("alice@example.com"));
    }

    // The gateway shapes a destination failure into a ProblemDetails naming the cluster, so the banner can say
    // which service is down rather than "something went wrong".
    [Fact]
    public async Task GetInbox_GatewayDestinationUnreachable_ThrowsNamingTheService()
    {
        var problem = """
            {"type":"gateway-destination-unreachable","failedService":"customer","status":502}
            """;
        var sut = new MessagingApiService(
            CreateFactory(HttpStatusCode.BadGateway, problem), CreateSession());

        var ex = await Assert.ThrowsAsync<GatewayServiceUnavailableException>(() => sut.GetInboxAsync());
        Assert.Equal("customer", ex.FailedService);
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

        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.Created, json), CreateSession());

        var result = await sut.SendMessageAsync("alice@example.com", "Hi there!");

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Message);
        Assert.Equal("m1", result.Message!.Id);
        Assert.Equal("t1", result.Message.ThreadId);
        Assert.Equal("Hi there!", result.Message.Body);
        Assert.False(result.Message.IsRead);
    }

    // A 201 the client could not bind still stored the message. Reporting a failure would invite a duplicate.
    [Fact]
    public async Task SendMessage_Returns201WithUnbindableBody_StillSucceeds()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.Created, "null"), CreateSession());

        var result = await sut.SendMessageAsync("alice@example.com", "Hi there!");

        Assert.True(result.Succeeded);
        Assert.Null(result.Message);
    }

    // THE reported symptom. POST /api/v1/messages answers 403 when the two parties are not on either side of
    // a subscription; collapsing that into the same null as a dropped connection made the app tell people to
    // check a network that was working.
    [Fact]
    public async Task SendMessage_Returns403_ReportsTheSubscriptionRuleNotTheConnection()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.Forbidden), CreateSession());

        var result = await sut.SendMessageAsync("stranger@example.com", "body");

        Assert.False(result.Succeeded);
        Assert.Equal(MessageSendResult.NotPermittedMessage, result.ErrorMessage);
        Assert.DoesNotContain("connection", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessage_Returns400_ReportsARejectionNotTheConnection()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.BadRequest), CreateSession());

        var result = await sut.SendMessageAsync("bad-email", "body");

        Assert.False(result.Succeeded);
        Assert.Equal(MessageSendResult.RejectedMessage, result.ErrorMessage);
        Assert.DoesNotContain("connection", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // The one case the connection IS implicated in, so it is the one case allowed to say so.
    [Fact]
    public async Task SendMessage_GatewayDestinationUnreachable_ReportsTheConnection()
    {
        var problem = """
            {"type":"gateway-destination-unreachable","failedService":"customer","status":502}
            """;
        var sut = new MessagingApiService(
            CreateFactory(HttpStatusCode.BadGateway, problem), CreateSession());

        var result = await sut.SendMessageAsync("alice@example.com", "body");

        Assert.False(result.Succeeded);
        Assert.Equal(MessageSendResult.UnreachableMessage, result.ErrorMessage);
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

        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.OK, json), CreateSession());

        var result = await sut.MarkReadAsync("m1");

        Assert.NotNull(result);
        Assert.Equal("m1", result!.Id);
        Assert.True(result.IsRead);
    }

    [Fact]
    public async Task MarkRead_Returns404_ReturnsNull()
    {
        var sut = new MessagingApiService(CreateFactory(HttpStatusCode.NotFound), CreateSession());

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
    // SentAt is persisted UTC but consumed as wall-clock (the thread formats it {0:h:mm tt}, TimeAgo
    // subtracts it from DateTime.Now). Left in UTC, a message sent seconds ago showed the wrong hour and a
    // negative "ago".
    [Fact]
    public void MessageSummary_SentAt_IsConvertedToLocalTime()
    {
        var utc = new DateTime(2026, 9, 3, 1, 23, 0, DateTimeKind.Utc);

        var summary = new MessageSummary { SentAt = utc };

        Assert.Equal(utc.ToLocalTime(), summary.SentAt);
        Assert.NotEqual(DateTimeKind.Utc, summary.SentAt.Kind);
    }

    [Fact]
    public void MessageSummary_SentAt_LeavesANonUtcValueAlone()
    {
        var local = new DateTime(2026, 9, 3, 19, 23, 0, DateTimeKind.Local);

        var summary = new MessageSummary { SentAt = local };

        Assert.Equal(local, summary.SentAt);
    }

}
