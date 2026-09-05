namespace AgendaBuddy.Library.Services;

public interface INotificationService
{
    /// <summary>
    /// Persists one inbox row. This is the in-app half only — see
    /// <see cref="INotificationDispatcher"/> for the one that also reaches a recipient who is not
    /// currently in the app.
    /// </summary>
    Task SendAsync(NotificationEntity notification);

    /// <summary>
    /// The recipient's newest notifications, newest first.
    /// </summary>
    /// <param name="limit">
    /// Maximum rows to return, clamped to <see cref="NotificationService.MaxPageSize"/>. The cap is applied here as well as at
    /// the endpoint because it is what bounds the read, and an in-process caller must not be able to ask
    /// for an unbounded one either.
    /// </param>
    /// <param name="unreadOnly">When true, read rows are excluded from both the page and its ordering.</param>
    Task<IEnumerable<NotificationEntity>> GetForRecipientAsync(
        string recipientEmail, int limit = NotificationService.DefaultPageSize, bool unreadOnly = false);

    /// <summary>How many of the recipient's notifications are unread. Counted in the database, not read back.</summary>
    Task<long> CountUnreadAsync(string recipientEmail);

    /// <summary>Marks one notification read. A no-op when it is already read or does not exist.</summary>
    Task MarkReadAsync(string notificationId);

    /// <summary>
    /// Marks every one of the recipient's unread notifications read, and returns how many changed.
    /// </summary>
    Task<long> MarkAllReadAsync(string recipientEmail);
}
