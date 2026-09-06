using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

/// <summary>
/// The fan-out across channels. The reason this exists is that an in-app inbox row reaches nobody who is not
/// already signed in and looking, which for a booking request is nobody.
/// </summary>
public class NotificationDispatcherTest
{
    private readonly Mock<INotificationService> _inbox = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<IPushSender> _push = new();
    private readonly Mock<IDeviceTokenService> _deviceTokens = new();
    private readonly NotificationDispatcher _dispatcher;

    public NotificationDispatcherTest()
    {
        _deviceTokens.Setup(d => d.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new DeviceTokenEntity { UserEmail = "r@example.com", Token = "device-token", Platform = "android" });

        _dispatcher = new NotificationDispatcher(
            _inbox.Object, _email.Object, _push.Object, _deviceTokens.Object);
    }

    private static NotificationEntity Notification(
        NotificationType type, string appointmentIdentifier = "appt-1") =>
        new("r@example.com", "Subject line", "Body line", type, appointmentIdentifier);

    [Theory]
    [InlineData(NotificationType.AppointmentRequested)]
    [InlineData(NotificationType.AppointmentBooked)]
    [InlineData(NotificationType.AppointmentUpdated)]
    [InlineData(NotificationType.AppointmentCancelled)]
    [InlineData(NotificationType.AppointmentCompleted)]
    public async Task AppointmentNotifications_GoToTheInboxAndEmailAndPush(NotificationType type)
    {
        await _dispatcher.DispatchAsync(Notification(type));

        _inbox.Verify(i => i.SendAsync(It.IsAny<NotificationEntity>()), Times.Once);
        _email.Verify(e => e.SendAsync("r@example.com", "Subject line", "Body line", It.IsAny<CancellationToken>()), Times.Once);
        // The push goes out, but NOT carrying the producer's strings — see the T-002 section below.
        _push.Verify(p => p.SendAsync(
            "device-token", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── T-002: nothing the OS displays may carry content ────────────────────────────────────────────
    // The mobile-app threat model's T-002 ("PII Exposed in Push Notification Lock-Screen Payload", approved
    // "mitigate now") requires the push notification body to be a generic status string, because a lock screen
    // renders notification.title/body with no authentication, in front of whoever is holding or standing near
    // the device. It had never been implemented: the producer's own subject and body were passed straight
    // through. These tests are the mitigation's only enforcement — the exposure is invisible from the code,
    // and it looks and behaves like a working notification.

    /// <summary>
    /// The producer's strings must not appear in what the OS draws — for <b>every</b> type, so a new producer
    /// inherits the safe default rather than having to know the rule exists.
    /// </summary>
    [Theory]
    [InlineData(NotificationType.AppointmentRequested)]
    [InlineData(NotificationType.AppointmentBooked)]
    [InlineData(NotificationType.AppointmentUpdated)]
    [InlineData(NotificationType.AppointmentCancelled)]
    [InlineData(NotificationType.AppointmentCompleted)]
    [InlineData(NotificationType.MessageReceived)]
    [InlineData(NotificationType.PasswordResetRequested)]
    [InlineData(NotificationType.EmailConfirmationRequested)]
    public async Task ThePushedTitleAndBodyNeverCarryTheProducersText(NotificationType type)
    {
        string? pushedTitle = null;
        string? pushedBody = null;
        _push.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, title, body, _, _) => { pushedTitle = title; pushedBody = body; })
            .ReturnsAsync(true);

        await _dispatcher.DispatchAsync(new NotificationEntity(
            "r@example.com",
            "New message from customer@example.com",
            "Are we still on for Friday? My address is 14 Elm Street.",
            type,
            "appt-1"));

        Assert.NotNull(pushedTitle);
        Assert.DoesNotContain("customer@example.com", pushedTitle!);
        Assert.DoesNotContain("customer@example.com", pushedBody!);
        Assert.DoesNotContain("Elm Street", pushedBody!);
        Assert.DoesNotContain("Friday", pushedBody!);
    }

    /// <summary>
    /// What is drawn instead: a category, from the type alone. Every declared type gets its own wording rather
    /// than falling through to the generic default, which would make a cancellation indistinguishable from a
    /// message.
    /// </summary>
    [Fact]
    public void EveryNotificationTypeHasItsOwnLockScreenSafeWording()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in Enum.GetValues<NotificationType>())
        {
            var (title, body) = NotificationDispatcher.DisplayText(type);

            Assert.False(string.IsNullOrWhiteSpace(title));
            Assert.False(string.IsNullOrWhiteSpace(body));
            seen.Add(title);
        }

        // The two auth types deliberately share one wording, so the distinct count is members minus one.
        Assert.Equal(Enum.GetValues<NotificationType>().Length - 1, seen.Count);
    }

    // An undeclared type must not fall back to anything content-bearing.
    [Fact]
    public void AnUnknownNotificationTypeStillSaysNothingAboutItsContent()
    {
        var (title, body) = NotificationDispatcher.DisplayText((NotificationType)9999);

        Assert.Equal("Notification", title);
        Assert.Contains("Open the app", body);
    }

    /// <summary>
    /// The detail is not lost — it travels in the <c>data</c> payload, which the OS hands to the app instead of
    /// drawing, so the in-app banner can render it behind authentication. This is T-002's own prescribed
    /// mechanism, not a workaround for it.
    /// </summary>
    [Fact]
    public async Task TheProducersRealTextTravelsInTheDataPayloadInstead()
    {
        IReadOnlyDictionary<string, string>? pushedData = null;
        _push.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, _, _, data, _) => pushedData = data)
            .ReturnsAsync(true);

        await _dispatcher.DispatchAsync(Notification(NotificationType.AppointmentRequested));

        Assert.NotNull(pushedData);
        Assert.Equal("Subject line", pushedData![PushPayloadKeys.Subject]);
        Assert.Equal("Body line", pushedData[PushPayloadKeys.Body]);
        Assert.Equal("appt-1", pushedData[PushPayloadKeys.AppointmentIdentifier]);
    }

    /// <summary>
    /// A notification with no appointment omits the key entirely rather than sending it empty, so the client's
    /// "did the caller have an appointment in hand" check stays a presence check.
    /// </summary>
    [Fact]
    public async Task ANotificationWithNoAppointment_OmitsThatKeyButStillCarriesTheDetail()
    {
        IReadOnlyDictionary<string, string>? pushedData = null;
        _push.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, _, _, data, _) => pushedData = data)
            .ReturnsAsync(true);

        await _dispatcher.DispatchAsync(
            Notification(NotificationType.MessageReceived, appointmentIdentifier: ""));

        Assert.NotNull(pushedData);
        Assert.False(pushedData!.ContainsKey(PushPayloadKeys.AppointmentIdentifier));
        Assert.Equal("Subject line", pushedData[PushPayloadKeys.Subject]);
    }

    /// <summary>
    /// The in-app inbox and email are unaffected. T-002 is about what an <b>operating system</b> displays
    /// without authentication — an inbox row is behind a sign-in, and an email is addressed to the recipient
    /// rather than broadcast to a screen. Genericising those would lose real information for no security gain.
    /// </summary>
    [Fact]
    public async Task TheInboxAndEmailStillCarryTheFullText()
    {
        await _dispatcher.DispatchAsync(Notification(NotificationType.AppointmentRequested));

        _inbox.Verify(i => i.SendAsync(It.Is<NotificationEntity>(
            n => n.Subject == "Subject line" && n.Body == "Body line")), Times.Once);
        _email.Verify(e => e.SendAsync(
            "r@example.com", "Subject line", "Body line", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A conversation that emails every line is what makes people mute a product, so a message notification
    /// takes the two channels that suit a conversation and not the one that does not.
    /// </summary>
    [Fact]
    public async Task MessageNotifications_GoToTheInboxAndPush_ButNotEmail()
    {
        await _dispatcher.DispatchAsync(Notification(NotificationType.MessageReceived, appointmentIdentifier: ""));

        _inbox.Verify(i => i.SendAsync(It.IsAny<NotificationEntity>()), Times.Once);
        _push.Verify(p => p.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Identity sends the confirmation and reset emails itself — they carry a token and wording this cannot
    /// supply — so dispatching them here would double-send.
    /// </summary>
    [Theory]
    [InlineData(NotificationType.PasswordResetRequested)]
    [InlineData(NotificationType.EmailConfirmationRequested)]
    public async Task AuthNotifications_AreNotEmailedByTheDispatcher(NotificationType type)
    {
        await _dispatcher.DispatchAsync(Notification(type, appointmentIdentifier: ""));

        _inbox.Verify(i => i.SendAsync(It.IsAny<NotificationEntity>()), Times.Once);
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The payload key is what lets a tapped push open the appointment instead of just the app. It has to match
    /// the client's <c>PushNotificationService.AppointmentIdentifierKey</c>.
    /// </summary>
    [Fact]
    public async Task Push_CarriesTheAppointmentIdentifierSoATapCanOpenIt()
    {
        IReadOnlyDictionary<string, string>? data = null;
        _push.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, _, _, d, _) => data = d)
            .ReturnsAsync(true);

        await _dispatcher.DispatchAsync(Notification(NotificationType.AppointmentRequested, "appt-42"));

        Assert.NotNull(data);
        Assert.Equal("appt-42", data!["appointmentIdentifier"]);
    }

    /// <summary>
    /// A notification with no appointment omits <b>that key</b> — it no longer sends no <c>data</c> at all.
    /// </summary>
    /// <remarks>
    /// This assertion used to be <c>Assert.Null(data)</c>, and the change is deliberate rather than a relaxation:
    /// since T-002, <c>data</c> always carries the producer's subject and body (that is where a notification's
    /// real content now lives, because the OS-drawn fields may not). The invariant worth keeping from the old
    /// version is the narrower one — the appointment key is <b>absent</b>, not present and empty — because the
    /// client treats its presence as "the caller had an appointment in hand". <c>FcmPushSender</c>'s own
    /// "FCM rejects <c>data: null</c>" behaviour is still covered where it belongs, in
    /// <c>FcmPushSenderTest.Send_OmitsTheDataKeyEntirelyWhenThereIsNoPayload</c>.
    /// </remarks>
    [Fact]
    public async Task Push_OmitsTheAppointmentKeyWhenThereIsNoAppointment()
    {
        IReadOnlyDictionary<string, string>? data = null;
        _push.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, _, _, d, _) => data = d)
            .ReturnsAsync(true);

        await _dispatcher.DispatchAsync(Notification(NotificationType.MessageReceived, appointmentIdentifier: ""));

        Assert.NotNull(data);
        Assert.False(data!.ContainsKey(PushPayloadKeys.AppointmentIdentifier));
    }

    [Fact]
    public async Task Push_IsSkippedWhenTheRecipientHasNoRegisteredDevice()
    {
        _deviceTokens.Setup(d => d.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((DeviceTokenEntity?)null);

        await _dispatcher.DispatchAsync(Notification(NotificationType.AppointmentRequested));

        _push.Verify(p => p.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
        // The inbox row still happened: one missing channel must not cost the others.
        _inbox.Verify(i => i.SendAsync(It.IsAny<NotificationEntity>()), Times.Once);
    }

    // ── Independence of channels ────────────────────────────────────────────────────────────────────
    // Every channel is best-effort and independent. A mail provider being down must not cost the push, and
    // none of them may fail the operation that triggered the notification.

    [Fact]
    public async Task ADeadInbox_DoesNotStopEmailOrPush()
    {
        _inbox.Setup(i => i.SendAsync(It.IsAny<NotificationEntity>()))
            .ThrowsAsync(new InvalidOperationException("mongo down"));

        await _dispatcher.DispatchAsync(Notification(NotificationType.AppointmentCancelled));

        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _push.Verify(p => p.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ADeadMailProvider_DoesNotStopPush()
    {
        _email.Setup(e => e.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("resend unreachable"));

        await _dispatcher.DispatchAsync(Notification(NotificationType.AppointmentCancelled));

        _push.Verify(p => p.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EveryChannelFailing_StillDoesNotThrow()
    {
        _inbox.Setup(i => i.SendAsync(It.IsAny<NotificationEntity>())).ThrowsAsync(new InvalidOperationException());
        _email.Setup(e => e.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException());
        _deviceTokens.Setup(d => d.GetByEmailAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());

        // The contract: dispatching never throws, because the appointment that triggered it has already
        // happened and must not be undone by a notification.
        await _dispatcher.DispatchAsync(Notification(NotificationType.AppointmentRequested));
    }

    [Fact]
    public async Task ANullNotification_IsIgnoredRatherThanThrowing()
    {
        await _dispatcher.DispatchAsync(null!);

        _inbox.Verify(i => i.SendAsync(It.IsAny<NotificationEntity>()), Times.Never);
    }
}
