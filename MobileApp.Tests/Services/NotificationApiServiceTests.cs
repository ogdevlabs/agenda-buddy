using System.Net;
using System.Text;
using AgendaBuddy.Library.Entities;
using MobileApp.Models;
using MobileApp.Services;
using Moq;
using Xunit;

namespace MobileApp.Tests.Services;

public class NotificationApiServiceTests
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
    // GetNotificationsAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetNotifications_Returns200_DeserializesList()
    {
        var json = """
            [
                {"id":"n1","notificationType":0,"message":"Your appointment has been booked.","createdAt":"2026-07-30T10:00:00Z","isRead":false},
                {"id":"n2","notificationType":2,"message":"Your appointment was cancelled.","createdAt":"2026-07-30T11:00:00Z","isRead":true}
            ]
            """;

        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.GetNotificationsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("n1", result[0].Id);
        Assert.Equal(NotificationType.AppointmentBooked, result[0].NotificationType);
        Assert.False(result[0].IsRead);
        Assert.Equal("n2", result[1].Id);
        Assert.Equal(NotificationType.AppointmentCancelled, result[1].NotificationType);
        Assert.True(result[1].IsRead);
    }

    [Fact]
    public async Task GetNotifications_Returns401_ReturnsEmptyList()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.Unauthorized));

        var result = await sut.GetNotificationsAsync();

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // MarkReadAsync tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MarkRead_Returns200_ReturnsUpdated()
    {
        var json = """
            {"id":"n1","notificationType":0,"message":"Your appointment has been booked.","createdAt":"2026-07-30T10:00:00Z","isRead":true}
            """;

        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, json));

        var result = await sut.MarkReadAsync("n1");

        Assert.NotNull(result);
        Assert.Equal("n1", result!.Id);
        Assert.True(result.IsRead);
    }

    [Fact]
    public async Task MarkRead_Returns404_ReturnsNull()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.NotFound));

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
