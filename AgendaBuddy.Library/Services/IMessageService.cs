namespace AgendaBuddy.Library.Services;

public interface IMessageService
{
    Task SendMessageAsync(MessageEntity message);
    Task<IEnumerable<MessageEntity>> GetThreadAsync(string senderEmail, string recipientEmail);
    Task<IEnumerable<MessageEntity>> GetInboxAsync(string recipientEmail);
    Task MarkReadAsync(string messageId);
}
