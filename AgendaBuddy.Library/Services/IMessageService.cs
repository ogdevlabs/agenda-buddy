namespace AgendaBuddy.Library.Services;

public interface IMessageService
{
    Task SendMessageAsync(MessageEntity message);
    Task<IEnumerable<MessageEntity>> GetThreadAsync(string senderEmail, string recipientEmail);
    /// <summary>Every message <paramref name="participantEmail"/> is a party to — sent as well as received.</summary>
    Task<IEnumerable<MessageEntity>> GetInboxAsync(string participantEmail);
    Task MarkReadAsync(string messageId);

    /// <summary>
    /// Marks every message <paramref name="counterpartEmail"/> sent to <paramref name="callerEmail"/> in their
    /// shared thread as read, and answers how many changed.
    /// </summary>
    /// <remarks>
    /// One write for the whole thread. Opening a conversation marks its unread set read, and doing that one
    /// message at a time is a request per message.
    /// </remarks>
    Task<long> MarkThreadReadAsync(string callerEmail, string counterpartEmail);
}
