using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface INotificationApiService
{
    Task<List<NotificationSummary>> GetNotificationsAsync(
        int? limit = null, bool unreadOnly = false, CancellationToken ct = default);

    /// <summary>The unread count alone, for the badge. Zero when the call fails — a badge is not worth an error banner.</summary>
    Task<long> GetUnreadCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks one notification read.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the server accepted it. The route answers 204 with no body, so there is nothing to
    /// return but success — and the caller must not update its own state on a failure, or the count drifts
    /// from what a reload will show.
    /// </returns>
    Task<bool> MarkReadAsync(string id, CancellationToken ct = default);

    /// <summary>Marks every unread notification read, returning how many the server changed.</summary>
    Task<long> MarkAllReadAsync(CancellationToken ct = default);
}
