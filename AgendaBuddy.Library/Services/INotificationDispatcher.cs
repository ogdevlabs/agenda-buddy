namespace AgendaBuddy.Library.Services;

/// <summary>
/// Delivers one notification on every channel its type calls for: the in-app inbox row always, plus email
/// and push where they apply.
/// </summary>
/// <remarks>
/// The reason this exists rather than each producer calling <see cref="INotificationService"/>,
/// <see cref="IEmailSender"/> and <see cref="IPushSender"/> in turn: the inbox is a pull-only surface behind
/// a login, so a notification that is only written there reaches nobody who is not already looking. Which
/// channels a type uses is a single policy decision, and a producer is the wrong place to re-take it.
/// <para>
/// Deliberately <b>not</b> folded into <see cref="INotificationService.SendAsync"/>. Identity sends its own
/// email for confirmation and reset — those carry a token and need wording this cannot supply — so a
/// service-level fan-out would double-send them.
/// </para>
/// </remarks>
public interface INotificationDispatcher
{
    /// <summary>
    /// Writes the inbox row, then fans out to whichever other channels the notification's type uses.
    /// </summary>
    /// <remarks>
    /// Never throws. Every channel is best-effort and independent: a failed email does not stop the push, and
    /// neither can fail the appointment that triggered them. The inbox row is attempted first because it is
    /// the only channel that is durable.
    /// </remarks>
    Task DispatchAsync(NotificationEntity notification, CancellationToken cancellationToken = default);
}
