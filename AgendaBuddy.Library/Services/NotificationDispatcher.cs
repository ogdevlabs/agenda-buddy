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

        // appointmentIdentifier is what lets a tapped notification open the appointment it is about, rather
        // than only the app. Absent for a message notification, which has no appointment.
        var data = string.IsNullOrWhiteSpace(notification.AppointmentIdentifier)
            ? null
            : new Dictionary<string, string> { ["appointmentIdentifier"] = notification.AppointmentIdentifier };

        return await pushSender.SendAsync(
            device.Token, notification.Subject, notification.Body, data, cancellationToken);
    }

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
