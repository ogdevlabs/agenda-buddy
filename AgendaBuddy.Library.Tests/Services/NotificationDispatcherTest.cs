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
        _push.Verify(p => p.SendAsync(
            "device-token", "Subject line", "Body line",
            It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task Push_OmitsTheDataPayloadWhenThereIsNoAppointment()
    {
        IReadOnlyDictionary<string, string>? data = new Dictionary<string, string>();
        _push.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, _, _, d, _) => data = d)
            .ReturnsAsync(true);

        await _dispatcher.DispatchAsync(Notification(NotificationType.MessageReceived, appointmentIdentifier: ""));

        Assert.Null(data);
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
