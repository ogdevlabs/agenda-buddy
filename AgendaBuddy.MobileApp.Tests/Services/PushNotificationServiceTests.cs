using System.Net;
using System.Net.Http;
using System.Text.Json;
using AgendaBuddy.MobileApp.Infrastructure;
using AgendaBuddy.MobileApp.Services;
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

    private static RecordingPushNotificationService Recording(List<string> navigated) =>
        new(Mock.Of<IHttpClientFactory>(), Mock.Of<ISecureStorageService>(), navigated);

    /// <summary>
    /// Captures the navigation instead of performing it — <c>Shell.Current</c> does not exist on the
    /// <c>net10.0</c> test slice.
    /// </summary>
    private sealed class RecordingPushNotificationService(
        IHttpClientFactory httpClientFactory,
        ISecureStorageService secureStorage,
        List<string> navigated)
        : PushNotificationService(httpClientFactory, secureStorage)
    {
        public override void HandleNotificationTap(string appointmentId) => navigated.Add(appointmentId);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }
}
