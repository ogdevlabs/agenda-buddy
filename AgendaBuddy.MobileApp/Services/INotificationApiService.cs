using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface INotificationApiService
{
    Task<List<NotificationSummary>> GetNotificationsAsync(
        int? limit = null, bool unreadOnly = false, CancellationToken ct = default);

    /// <summary>
    /// The unread count alone, for the badge.
    /// </summary>
    /// <returns>
    /// The count, or <c>null</c> when it could not be read. <b>Not zero</b>: a failed request and "nothing
    /// unread" are different answers, and returning zero for the first is how a transient network blip used to
    /// clear a badge that had unread notifications behind it — telling the user there was nothing waiting.
    /// A badge is still not worth an error banner, so the failure is reported as "unknown" rather than thrown.
    /// </returns>
    Task<long?> GetUnreadCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks one notification read.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the server accepted it. The route answers 204 with no body, so there is nothing to
    /// return but success — and the caller must not update its own state on a failure, or the count drifts
    /// from what a reload will show.
    /// </returns>
    Task<bool> MarkReadAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Marks every unread notification read.
    /// </summary>
    /// <returns>
    /// How many rows the server changed, or <c>null</c> when the request itself failed. The two have to be
    /// distinguishable: a caller that cannot tell "the server marked nothing" from "the server was not reached"
    /// has to pick one message for both, and either choice is wrong half the time.
    /// </returns>
    Task<long?> MarkAllReadAsync(CancellationToken ct = default);
}
