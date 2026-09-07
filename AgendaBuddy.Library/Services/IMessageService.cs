namespace AgendaBuddy.Library.Services;

public interface IMessageService
{
    Task SendMessageAsync(MessageEntity message);
    Task<IEnumerable<MessageEntity>> GetThreadAsync(string senderEmail, string recipientEmail);
    /// <summary>Every message <paramref name="participantEmail"/> is a party to — sent as well as received.</summary>
    Task<IEnumerable<MessageEntity>> GetInboxAsync(string participantEmail);
    Task MarkReadAsync(string messageId);
}
