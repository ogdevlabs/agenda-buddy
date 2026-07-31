namespace Library.Services;

public interface INotificationService
{
    Task SendAsync(NotificationEntity notification);
    Task<IEnumerable<NotificationEntity>> GetForRecipientAsync(string recipientEmail);
    Task MarkReadAsync(string notificationId);
}
