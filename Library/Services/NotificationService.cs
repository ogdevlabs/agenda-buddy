namespace Library.Services;

public class NotificationService(IRepository<NotificationEntity> repository) : INotificationService
{
    public async Task SendAsync(NotificationEntity notification)
    {
        notification.Id = ObjectId.GenerateNewId();
        await repository.InsertAsync(notification);
    }

    public async Task<IEnumerable<NotificationEntity>> GetForRecipientAsync(string recipientEmail)
    {
        var filter = new BsonDocument("recipient_email", recipientEmail);
        return await repository.FindAllAsync(filter);
    }

    public async Task MarkReadAsync(string notificationId)
    {
        var notification = await repository.GetByIdAsync(notificationId);
        if (notification is null) return;
        notification.IsRead = true;
        await repository.UpdateAsync(notificationId, notification);
    }
}
