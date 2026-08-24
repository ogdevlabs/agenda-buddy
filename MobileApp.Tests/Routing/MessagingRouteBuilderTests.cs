using System.Text.Json;
using MobileApp.Routing;
using Xunit;

namespace MobileApp.Tests.Routing;

public class MessagingRouteBuilderTests
{
    // Pins MessagingApiService.GetInboxAsync's current route: GET "messages".
    [Fact]
    public void Inbox_BuildsGet()
    {
        var route = MessagingRouteBuilder.Inbox();

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("messages", route.Path);
    }

    // Pins MessagingApiService.GetThreadAsync's current route: GET "messages/thread/{threadId}".
    [Fact]
    public void Thread_BuildsGetByThreadId()
    {
        var route = MessagingRouteBuilder.Thread("t1");

        Assert.Equal(HttpMethod.Get, route.Method);
        Assert.Equal("messages/thread/t1", route.Path);
    }

    // Pins MessagingApiService.SendMessageAsync's current route: POST "messages".
    [Fact]
    public void SendMessage_BuildsPost()
    {
        var route = MessagingRouteBuilder.SendMessage();

        Assert.Equal(HttpMethod.Post, route.Method);
        Assert.Equal("messages", route.Path);
    }

    // Pins MessagingApiService.SendMessageAsync's current body shape: { recipientEmail, body }.
    [Fact]
    public void BuildSendMessagePayload_SerializesRecipientEmailAndBody()
    {
        var payload = MessagingRouteBuilder.BuildSendMessagePayload("alice@example.com", "Hi there!");

        var json = JsonSerializer.Serialize(payload);

        Assert.Equal("""{"recipientEmail":"alice@example.com","body":"Hi there!"}""", json);
    }

    // Pins MessagingApiService.MarkReadAsync's current route: PATCH "messages/{id}/read".
    [Fact]
    public void MarkRead_BuildsPatchById()
    {
        var route = MessagingRouteBuilder.MarkRead("m1");

        Assert.Equal(HttpMethod.Patch, route.Method);
        Assert.Equal("messages/m1/read", route.Path);
    }
}
