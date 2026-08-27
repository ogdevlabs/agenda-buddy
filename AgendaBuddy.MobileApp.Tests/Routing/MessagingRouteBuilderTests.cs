using System.Text.Json;
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
}
