using Microsoft.Extensions.Logging;

namespace AgendaBuddy.Library.Services;

/// <inheritdoc cref="INotificationDispatcher"/>
public class NotificationDispatcher(
    INotificationService notificationService,
    IEmailSender emailSender,
    IPushSender pushSender,
    IDeviceTokenService deviceTokenService,
    ILogger<NotificationDispatcher>? logger = null) : INotificationDispatcher
{
    /// <summary>
    /// The types that also go out by email.
    /// </summary>
    /// <remarks>
    /// Appointment lifecycle only. A message notification is deliberately absent: a chat that emails on every
    /// line is the reason people mute a product, and push is the channel that fits a conversation. The two
    /// auth types are absent because Identity sends their email itself — those messages carry a token and
    /// wording this cannot supply, and dispatching them here would double-send.
    /// </remarks>
    private static readonly HashSet<NotificationType> EmailedTypes =
    [
        NotificationType.AppointmentRequested,
        NotificationType.AppointmentBooked,
        NotificationType.AppointmentUpdated,
        NotificationType.AppointmentCancelled,
        NotificationType.AppointmentCompleted
    ];

    public async Task DispatchAsync(NotificationEntity notification, CancellationToken cancellationToken = default)
    {
        if (notification is null) return;

        // First, and on its own try: it is the only durable channel, and the one the app reads back.
        try
        {
            await notificationService.SendAsync(notification);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "notification.inbox-write-failed: {Type}", notification.Type);
        }

        if (EmailedTypes.Contains(notification.Type))
            await TryAsync(
                () => emailSender.SendAsync(
                    notification.RecipientEmail, notification.Subject, notification.Body, cancellationToken),
                "email", notification.Type);

        await TryAsync(() => PushAsync(notification, cancellationToken), "push", notification.Type);
    }

    private async Task<bool> PushAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        var device = await deviceTokenService.GetByEmailAsync(notification.RecipientEmail);
        if (device is null || string.IsNullOrWhiteSpace(device.Token)) return false;

        // The producer's real text goes in `data`, which the OS delivers to the app instead of drawing — so it
        // is only ever rendered behind authentication. The displayed title/body come from the type alone.
        var data = new Dictionary<string, string>
        {
            [PushPayloadKeys.Subject] = notification.Subject,
            [PushPayloadKeys.Body] = notification.Body
        };

        // Absent for a message notification, which has no appointment. Omitted rather than sent empty, so the
        // client's "did the caller have an appointment in hand" check stays a presence check.
        if (!string.IsNullOrWhiteSpace(notification.AppointmentIdentifier))
            data[PushPayloadKeys.AppointmentIdentifier] = notification.AppointmentIdentifier;

        var (title, body) = DisplayText(notification.Type);

        return await pushSender.SendAsync(device.Token, title, body, data, cancellationToken);
    }

    /// <summary>
    /// What the operating system is allowed to draw: a category, and never the notification's content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Threat T-002 (mobile-app threat model, approved "mitigate now").</b> A lock screen renders
    /// <c>notification.title</c>/<c>notification.body</c> with no authentication, in front of anyone holding or
    /// standing near the device. Passing the producer's own strings through put real PII there: the booking
    /// bodies name the counterparty's email address, the service and the appointment time, and
    /// <c>MessageModule</c>'s notification put the sender's address in the <i>title</i> and a 120-character
    /// preview of the message itself in the body — a private conversation, on a locked screen.
    /// </para>
    /// <para>
    /// Derived from <see cref="NotificationType"/> and nothing else, deliberately. A producer cannot opt out of
    /// this by writing a different subject, and a new producer gets the safe default without knowing the rule
    /// exists — which is the only version of this that stays true. The detail is not lost: it is carried in the
    /// <c>data</c> payload and rendered in-app once the reader is past the lock screen, exactly as T-002's
    /// approved mitigation describes.
    /// </para>
    /// <para>
    /// The in-app inbox and email are unaffected — the inbox is behind authentication, and an email is addressed
    /// to the recipient rather than broadcast to a screen. T-002 is specifically about what the OS displays.
    /// </para>
    /// </remarks>
    public static (string Title, string Body) DisplayText(NotificationType type) => type switch
    {
        NotificationType.AppointmentRequested =>
            ("Appointment request", "Someone has requested an appointment. Open the app for details."),
        NotificationType.AppointmentBooked =>
            ("Appointment confirmed", "An appointment has been confirmed. Open the app for details."),
        NotificationType.AppointmentUpdated =>
            ("Appointment updated", "An appointment has changed. Open the app for details."),
        NotificationType.AppointmentCancelled =>
            ("Appointment cancelled", "An appointment has been cancelled. Open the app for details."),
        NotificationType.AppointmentCompleted =>
            ("Appointment completed", "An appointment has been marked complete. Open the app for details."),
        NotificationType.MessageReceived =>
            ("New message", "You have a new message. Open the app to read it."),
        NotificationType.PasswordResetRequested =>
            ("Security alert", "There is a security update on your account. Open the app for details."),
        NotificationType.EmailConfirmationRequested =>
            ("Security alert", "There is a security update on your account. Open the app for details."),
        // A type nobody has written display text for still says nothing about its content. The safe answer is
        // the default, not the producer's string.
        _ => ("Notification", "You have a new notification. Open the app for details.")
    };

    /// <summary>
    /// Runs one channel, absorbing its failure. Channels are independent on purpose: a mail provider being
    /// down must not cost the push as well.
    /// </summary>
    private async Task TryAsync(Func<Task<bool>> send, string channel, NotificationType type)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            // The type, never the recipient or the body — the body names an appointment and the recipient is
            // an email address, which is what PiiRedactingProcessor exists to keep out of exported telemetry.
            logger?.LogWarning(ex, "notification.{Channel}-failed: {Type}", channel, type);
        }
    }
}
