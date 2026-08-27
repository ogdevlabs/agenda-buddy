using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface INotificationApiService
{
    Task<List<NotificationSummary>> GetNotificationsAsync(CancellationToken ct = default);
    Task<NotificationSummary?> MarkReadAsync(string id, CancellationToken ct = default);
}
