using System.Text.Json;
using AgendaBuddy.Library.Services;
using AgendaBuddy.MobileApp.Routing;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Routing;

public class MessagingRouteBuilderTests
{
    // The real backend route is hosted by Customer under a top-level /api/v1/messages group
    // (ADR D-2), not nested under /api/v1/customers.
    [Fact]
    public void Inbox_BuildsGet()
    {
        var route = MessagingRouteBuilder.Inbox();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/messages", route.Path);
    }

    // The real route keys on the counterpart's email, not an opaque thread id.
    [Fact]
    public void Thread_BuildsGetByCounterpartEmail()
    {
        var route = MessagingRouteBuilder.Thread("alice@example.com");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("api/v1/messages/thread/alice@example.com", route.Path);
    }

    [Fact]
    public void SendMessage_BuildsPost()
    {
        var route = MessagingRouteBuilder.SendMessage();

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/messages", route.Path);
    }

    [Fact]
    public void BuildSendMessagePayload_SerializesRecipientEmailAndBody()
    {
        var payload = MessagingRouteBuilder.BuildSendMessagePayload("alice@example.com", "Hi there!");

        var json = JsonSerializer.Serialize(payload);

        Assert.Equal("""{"recipientEmail":"alice@example.com","body":"Hi there!"}""", json);
    }

    // POST, not PATCH — the real route is messages.MapPost("/{id}/read", …).
    [Fact]
    public void MarkRead_BuildsPostById()
    {
        var route = MessagingRouteBuilder.MarkRead("m1");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/messages/m1/read", route.Path);
    }

    [Fact]
    public void MarkThreadRead_BuildsPostByCounterpartEmail()
    {
        var route = MessagingRouteBuilder.MarkThreadRead("alice@example.com");

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("api/v1/messages/thread/alice@example.com/read", route.Path);
    }

    // Three segments against MarkRead's two, so the routes cannot shadow each other however Minimal API
    // orders them.
    [Fact]
    public void MarkThreadRead_CannotCollideWithMarkRead()
    {
        var thread = MessagingRouteBuilder.MarkThreadRead("alice@example.com").Path;
        var single = MessagingRouteBuilder.MarkRead("m1").Path;

        Assert.Equal(4, thread.Split('/').Length - 2);
        Assert.NotEqual(thread.Split('/').Length, single.Split('/').Length);
    }

    // The compose box, the send handler and the route all cap the body, and they must agree — the client
    // constant is what stops a message being sent only to be refused.
    [Fact]
    public void MaxBodyLength_MatchesTheServersCap()
    {
        Assert.Equal(MessageService.MaxBodyLength, MessagingRouteBuilder.MaxBodyLength);
    }
}
