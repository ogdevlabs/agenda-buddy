using System.Net;
using System.Text;
using System.Text.Json;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using MongoDB.Bson;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

public class NotificationApiServiceTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static FakeHttpMessageHandler _lastHandler = null!;

    private static IHttpClientFactory CreateFactory(HttpStatusCode statusCode, string? jsonContent = null)
    {
        var content = jsonContent is not null
            ? new StringContent(jsonContent, Encoding.UTF8, "application/json")
            : new StringContent(string.Empty);

        _lastHandler = new FakeHttpMessageHandler(statusCode, content);
        var client = new HttpClient(_lastHandler) { BaseAddress = new Uri("https://localhost/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(client);
        return factory.Object;
    }

    /// <summary>
    /// The bytes the real route actually returns: a <see cref="NotificationEntity"/> serialised with
    /// ASP.NET's web defaults plus the <see cref="ObjectIdJsonConverter"/> Customer registers.
    /// </summary>
    /// <remarks>
    /// ⚠️ Built from the entity rather than hand-written, and that is the entire point. The previous version of
    /// this test fed literal JSON in the shape the client wished for (<c>notificationType</c>, <c>message</c>) —
    /// a shape no route has ever emitted. It passed while the real screen showed a blank card labelled "Booked"
    /// for every notification including cancellations, because nothing compared the client's field names to
    /// the server's. Anything asserted through this helper is asserted against the server's contract.
    /// </remarks>
    private static string WireJson(params NotificationEntity[] notifications)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ObjectIdJsonConverter());
        return JsonSerializer.Serialize(notifications, options);
    }

    private static NotificationEntity Entity(
        NotificationType type,
        string subject,
        string body,
        string appointmentIdentifier = "",
        bool isRead = false) =>
        new(recipientEmail: "sarah@agendabuddy.dev", subject, body, type, appointmentIdentifier)
        {
            Id = ObjectId.GenerateNewId(),
            CreatedAt = new DateTime(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc),
            IsRead = isRead
        };

    // ---------------------------------------------------------------------------
    // GetNotificationsAsync
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Every field the UI renders has to survive the round trip from the real entity shape: the subject and
    /// body carry the whole message, and the type drives the category label.
    /// </summary>
    [Fact]
    public async Task GetNotifications_DeserializesTheShapeTheRouteActuallyReturns()
    {
        var requested = Entity(
            NotificationType.AppointmentRequested,
            "New appointment request",
            "customer@example.com requested Deep Tissue on Friday 5 September at 2:00 PM.",
            appointmentIdentifier: "appt-42");
        var cancelled = Entity(
            NotificationType.AppointmentCancelled,
            "Appointment cancelled",
            "Deep Tissue with provider@example.com on Friday 5 September at 2:00 PM was cancelled.",
            isRead: true);

        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, WireJson(requested, cancelled)));

        var result = await sut.GetNotificationsAsync();

        Assert.Equal(2, result.Count);

        Assert.Equal(requested.Id.ToString(), result[0].Id);
        Assert.Equal("New appointment request", result[0].Subject);
        Assert.Equal(requested.Body, result[0].Body);
        Assert.Equal(NotificationType.AppointmentRequested, result[0].Type);
        Assert.Equal("appt-42", result[0].AppointmentIdentifier);
        Assert.False(result[0].IsRead);

        // The regression that mattered: a cancellation must not present as a booking.
        Assert.Equal(NotificationType.AppointmentCancelled, result[1].Type);
        Assert.Equal("Cancelled", result[1].TypeLabel);
        Assert.True(result[1].IsRead);
    }

    /// <summary>
    /// The displayed strings, not just the parsed fields — a card whose title and message are empty is what the
    /// mismatch actually produced, and that is invisible if a test only checks the raw properties.
    /// </summary>
    [Fact]
    public async Task GetNotifications_ProducesANonEmptyTitleAndMessageForEveryRow()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, WireJson(
            Entity(NotificationType.AppointmentRequested, "New appointment request", "Someone requested a session."),
            Entity(NotificationType.MessageReceived, "New message from a@b.dev", "Are we still on for Friday?"))));

        var result = await sut.GetNotificationsAsync();

        Assert.All(result, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Title));
            Assert.False(string.IsNullOrWhiteSpace(row.Message));
            Assert.NotEqual("Info", row.TypeLabel);
        });
    }

    [Fact]
    public async Task GetNotifications_Returns401_ReturnsEmptyList()
    {
        // 401 is JwtDelegatingHandler's to act on -- it raises UnauthorizedAccess and the Shell goes to login,
        // so there is no point raising a banner on a screen that is being replaced.
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.Unauthorized));

        var result = await sut.GetNotificationsAsync();

        Assert.Empty(result);
    }

    /// <summary>
    /// A server error is not an empty inbox. Returning an empty list here rendered "No notifications yet" over
    /// a failed read, telling the user something false about their own account.
    /// </summary>
    [Fact]
    public async Task GetNotifications_ServerError_Throws_SoTheBannerCanShow()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetNotificationsAsync());
    }

    [Fact]
    public async Task GetNotifications_SendsLimitAndUnreadOnlyWhenAsked()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, "[]"));

        await sut.GetNotificationsAsync(limit: 25, unreadOnly: true);

        Assert.Contains("limit=25", _lastHandler.LastRequestUri!.Query);
        Assert.Contains("unreadOnly=true", _lastHandler.LastRequestUri.Query);
    }

    [Fact]
    public async Task GetNotifications_SendsNoQueryStringByDefault()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, "[]"));

        await sut.GetNotificationsAsync();

        Assert.Empty(_lastHandler.LastRequestUri!.Query);
    }

    // ---------------------------------------------------------------------------
    // Unread count
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetUnreadCount_ReadsTheCountRoute()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, """{"unreadCount":7}"""));

        var count = await sut.GetUnreadCountAsync();

        Assert.Equal(7, count);
        Assert.EndsWith("/api/v1/notifications/unread-count", _lastHandler.LastRequestUri!.AbsolutePath);
    }

    // A badge is decoration. It must never be the reason a screen shows an error.
    [Fact]
    public async Task GetUnreadCount_OnFailure_ReportsZeroRatherThanThrowing()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.InternalServerError));

        Assert.Equal(0, await sut.GetUnreadCountAsync());
    }

    // ---------------------------------------------------------------------------
    // MarkReadAsync
    // ---------------------------------------------------------------------------

    // The route answers 204 No Content, so success is the whole result -- there is no entity to return, and
    // the previous signature pretending otherwise is why an empty body had to be special-cased.
    [Fact]
    public async Task MarkRead_Returns204_ReportsSuccess()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.NoContent));

        Assert.True(await sut.MarkReadAsync("n1"));
        Assert.EndsWith("/api/v1/notifications/n1/read", _lastHandler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task MarkRead_Returns403_ReportsFailure()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.Forbidden));

        Assert.False(await sut.MarkReadAsync("someone-elses-notification"));
    }

    // ---------------------------------------------------------------------------
    // MarkAllReadAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MarkAllRead_ReturnsHowManyChanged()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.OK, """{"markedRead":4}"""));

        Assert.Equal(4, await sut.MarkAllReadAsync());
        Assert.EndsWith("/api/v1/notifications/read-all", _lastHandler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task MarkAllRead_OnFailure_ReportsZero()
    {
        var sut = new NotificationApiService(CreateFactory(HttpStatusCode.InternalServerError));

        Assert.Equal(0, await sut.MarkAllReadAsync());
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

        /// <summary>The URI of the most recent request, so a test can assert on the route and query actually sent.</summary>
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = _content });
        }
    }
}
