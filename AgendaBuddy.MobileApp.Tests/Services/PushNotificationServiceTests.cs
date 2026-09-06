using System.Net;
using System.Net.Http;
using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

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

    // ── Tap payload ─────────────────────────────────────────────────────────────────────────────────
    // The payload half of the tap is testable on the net10.0 slice; the platform event wiring is not, and is
    // only compiled under net10.0-android. These pin the CONTRACT with the server: the key here has to match
    // NotificationDispatcher's, or a tapped push opens the app and stops there.

    /// <summary>
    /// The key has to be exactly what <c>NotificationDispatcher</c> puts in the FCM data payload. Nothing else
    /// couples the two, so a rename on either side is silent.
    /// </summary>
    [Fact]
    public void TheAppointmentPayloadKeyMatchesTheOneTheServerSends()
    {
        Assert.Equal("appointmentIdentifier", PushNotificationService.AppointmentIdentifierKey);
    }

    [Fact]
    public void OnNotificationTapped_WithAnAppointmentIdentifier_RoutesToIt()
    {
        var navigated = new List<string>();
        var service = Recording(navigated);

        service.OnNotificationTapped(new Dictionary<string, string>
        {
            [PushNotificationService.AppointmentIdentifierKey] = "appt-42"
        });

        Assert.Equal("appt-42", Assert.Single(navigated));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OnNotificationTapped_WithNoUsableIdentifier_DoesNothing(string? identifier)
    {
        var navigated = new List<string>();
        var service = Recording(navigated);

        // A message notification carries no appointment, so the tap must open the app and stop there rather
        // than navigating to an empty route.
        service.OnNotificationTapped(identifier is null
            ? null
            : new Dictionary<string, string> { [PushNotificationService.AppointmentIdentifierKey] = identifier });

        Assert.Empty(navigated);
    }

    [Fact]
    public void OnNotificationTapped_WithAPayloadThatNamesNoAppointment_DoesNothing()
    {
        var navigated = new List<string>();
        var service = Recording(navigated);

        service.OnNotificationTapped(new Dictionary<string, string> { ["somethingElse"] = "appt-42" });

        Assert.Empty(navigated);
    }

    // ── Foreground arrival ──────────────────────────────────────────────────────────────────────────
    // Neither platform's OS draws a banner for a push that arrives while the app is on screen: Android hands
    // the message to the app instead of the tray, iOS asks the app what to present and shows nothing by
    // default. So a notification landing while somebody is USING the app was completely silent -- the badge did
    // not move and nothing appeared. These cover the half of that which is reachable on the net10.0 slice.

    [Fact]
    public void OnNotificationReceived_AnnouncesTheArrivalAndMovesTheBadge()
    {
        var service = ArrivalRecorder(out var badge, out var announced);

        service.OnNotificationReceived("New appointment request", "a@b.dev requested Friday 2:00 PM", null);

        var arrival = Assert.Single(announced);
        Assert.Equal("New appointment request", arrival.Title);
        Assert.Equal("a@b.dev requested Friday 2:00 PM", arrival.Body);
        Assert.Equal(1, badge.UnreadCount);
    }

    /// <summary>
    /// FCM's shape for a message that carries only state is a data payload with no notification block. Drawing
    /// an empty banner for one is worse than drawing nothing, and counting it would inflate the badge against a
    /// row the inbox does not have.
    /// </summary>
    [Fact]
    public void OnNotificationReceived_WithNoTitleAndNoBody_AnnouncesNothingAndLeavesTheBadgeAlone()
    {
        var service = ArrivalRecorder(out var badge, out var announced);

        service.OnNotificationReceived(null, "   ", new Dictionary<string, string> { ["state"] = "x" });

        Assert.Empty(announced);
        Assert.Equal(0, badge.UnreadCount);
    }

    [Fact]
    public void OnNotificationReceived_CarriesTheAppointmentIdentifierThroughToTheBanner()
    {
        var service = ArrivalRecorder(out _, out var announced);

        service.OnNotificationReceived(
            "Appointment cancelled",
            "Friday 2:00 PM is off",
            new Dictionary<string, string> { [PushNotificationService.AppointmentIdentifierKey] = "appt-42" });

        Assert.Equal("appt-42", Assert.Single(announced).AppointmentIdentifier);
    }

    /// <summary>
    /// The banner has to lead somewhere. An appointment notification goes to the appointment; a message
    /// notification names none, and must fall back to the inbox rather than shipping a banner whose only
    /// affordance is dismissing it.
    /// </summary>
    [Fact]
    public async Task Announce_WithAnAppointment_OffersAWayIntoIt()
    {
        var alerts = new CapturingAlertService();
        var navigated = new List<string>();
        var service = new RecordingPushNotificationService(
            Mock.Of<IHttpClientFactory>(), Mock.Of<ISecureStorageService>(), navigated, alerts);

        await service.AnnounceAsync(new InAppNotification("Appointment cancelled", "Friday is off", "appt-42"));

        Assert.Equal(PushNotificationService.ViewActionLabel, alerts.LastActionLabel);
        await alerts.InvokeLastActionAsync();

        Assert.Equal("appt-42", Assert.Single(navigated));
        Assert.Equal(0, service.InboxOpened);
    }

    [Fact]
    public async Task Announce_WithNoAppointment_OffersTheInboxInstead()
    {
        var alerts = new CapturingAlertService();
        var navigated = new List<string>();
        var service = new RecordingPushNotificationService(
            Mock.Of<IHttpClientFactory>(), Mock.Of<ISecureStorageService>(), navigated, alerts);

        await service.AnnounceAsync(new InAppNotification("New message from a@b.dev", "See you then", string.Empty));

        Assert.Equal(PushNotificationService.ViewActionLabel, alerts.LastActionLabel);
        await alerts.InvokeLastActionAsync();

        Assert.Empty(navigated);
        Assert.Equal(1, service.InboxOpened);
    }

    /// <summary>
    /// The banner has one text slot, so the subject leads and truncation costs the detail rather than the point.
    /// </summary>
    [Fact]
    public void TheBannerReadsAsSubjectThenDetail()
    {
        Assert.Equal(
            "New appointment request — a@b.dev requested Friday",
            new InAppNotification("New appointment request", "a@b.dev requested Friday", string.Empty)
                .BannerText);
    }

    // A body-only push is promoted to the banner's first line rather than rendering a blank one above it.
    [Fact]
    public void ABodyWithNoSubjectBecomesTheBannerLine()
    {
        var arrival = InAppNotification.From(
            null, "a@b.dev requested Friday", null, PushNotificationService.AppointmentIdentifierKey);

        Assert.NotNull(arrival);
        Assert.Equal("a@b.dev requested Friday", arrival!.Title);
        Assert.Equal("a@b.dev requested Friday", arrival.BannerText);
    }

    // ── Token rotation ──────────────────────────────────────────────────────────────────────────────
    // The app only ever registered the token it held at sign-in. FCM rotates tokens, and after a rotation
    // every send answered 404 against the stored one -- push was dead for the rest of the session with nothing
    // reporting it.

    [Fact]
    public async Task OnTokenChanged_RegistersTheNewToken()
    {
        var handler = new TestableHttpMessageHandler(HttpStatusCode.OK);
        var sut = ServiceWith(handler);

        await sut.OnTokenChangedAsync("rotated-token");

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("rotated-token", doc.RootElement.GetProperty("token").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OnTokenChanged_WithNothingUsable_PostsNothing(string? token)
    {
        var handler = new TestableHttpMessageHandler(HttpStatusCode.OK);
        var sut = ServiceWith(handler);

        await sut.OnTokenChangedAsync(token);

        Assert.Null(handler.LastRequest);
    }

    /// <summary>
    /// A rotation event that resolves to the token already registered is not worth a request. Only an accepted
    /// registration counts as registered, so a rejected one is retried rather than suppressed as already-done.
    /// </summary>
    [Fact]
    public async Task OnTokenChanged_RepeatingAnAcceptedToken_PostsOnlyOnce()
    {
        var handler = new CountingHttpMessageHandler(HttpStatusCode.OK);
        var sut = ServiceWith(handler);

        await sut.OnTokenChangedAsync("same-token");
        await sut.OnTokenChangedAsync("same-token");

        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task OnTokenChanged_AfterARejectedRegistration_TriesAgain()
    {
        var handler = new CountingHttpMessageHandler(HttpStatusCode.InternalServerError);
        var sut = ServiceWith(handler);

        await sut.OnTokenChangedAsync("same-token");
        await sut.OnTokenChangedAsync("same-token");

        Assert.Equal(2, handler.Requests);
    }

    private static PushNotificationService ServiceWith(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("AgendaBuddyApi")).Returns(httpClient);

        return new PushNotificationService(factory.Object, Mock.Of<ISecureStorageService>());
    }

    private static ArrivalRecordingPushNotificationService ArrivalRecorder(
        out NotificationBadgeViewModel badge, out List<InAppNotification> announced)
    {
        var api = new Mock<INotificationApiService>();
        // The badge reconciles against the server after an arrival; "unknown" leaves the local increment alone,
        // which is what these assertions are reading.
        api.Setup(a => a.GetUnreadCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);

        badge = new NotificationBadgeViewModel(api.Object);
        announced = new List<InAppNotification>();

        return new ArrivalRecordingPushNotificationService(
            Mock.Of<IHttpClientFactory>(), Mock.Of<ISecureStorageService>(), badge, announced);
    }

    private static RecordingPushNotificationService Recording(List<string> navigated) =>
        new(Mock.Of<IHttpClientFactory>(), Mock.Of<ISecureStorageService>(), navigated);

    /// <summary>Captures the arrival instead of presenting it — there is no MAUI presenter on this slice.</summary>
    private sealed class ArrivalRecordingPushNotificationService(
        IHttpClientFactory httpClientFactory,
        ISecureStorageService secureStorage,
        NotificationBadgeViewModel badge,
        List<InAppNotification> announced)
        : PushNotificationService(httpClientFactory, secureStorage, badge)
    {
        protected internal override Task AnnounceAsync(InAppNotification arrival)
        {
            announced.Add(arrival);
            return Task.CompletedTask;
        }
    }

    /// <summary>Holds on to the last banner so a test can fire its action and see where it leads.</summary>
    private sealed class CapturingAlertService : IInAppAlertService
    {
        public string? LastMessage { get; private set; }
        public string? LastActionLabel { get; private set; }
        private Func<Task>? _lastAction;

        public Task ShowAsync(string message)
        {
            LastMessage = message;
            LastActionLabel = null;
            _lastAction = null;
            return Task.CompletedTask;
        }

        public Task ShowAsync(string message, string actionLabel, Func<Task> action)
        {
            LastMessage = message;
            LastActionLabel = actionLabel;
            _lastAction = action;
            return Task.CompletedTask;
        }

        public Task InvokeLastActionAsync() => _lastAction?.Invoke() ?? Task.CompletedTask;
    }

    private sealed class CountingHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    /// <summary>
    /// Captures the navigation instead of performing it — <c>Shell.Current</c> does not exist on the
    /// <c>net10.0</c> test slice.
    /// </summary>
    private sealed class RecordingPushNotificationService(
        IHttpClientFactory httpClientFactory,
        ISecureStorageService secureStorage,
        List<string> navigated,
        IInAppAlertService? alerts = null)
        : PushNotificationService(httpClientFactory, secureStorage, badge: null, alerts: alerts)
    {
        public int InboxOpened { get; private set; }

        public override void HandleNotificationTap(string appointmentId) => navigated.Add(appointmentId);

        public override void HandleOpenInbox() => InboxOpened++;
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }
}
