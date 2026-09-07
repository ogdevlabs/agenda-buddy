using System.Linq;

namespace AgendaBuddy.Library.Services;

public class MessageService(IRepository<MessageEntity> repository) : IMessageService
{
    public async Task SendMessageAsync(MessageEntity message)
    {
        message.Id = ObjectId.GenerateNewId();
        // thread_id groups all messages between two participants
        var participants = new[] { message.SenderEmail, message.RecipientEmail }
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        message.ThreadId = $"{participants[0]}::{participants[1]}";
        await repository.InsertAsync(message);
    }

    public async Task<IEnumerable<MessageEntity>> GetThreadAsync(string senderEmail, string recipientEmail)
    {
        var participants = new[] { senderEmail, recipientEmail }
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var threadId = $"{participants[0]}::{participants[1]}";
        var filter = new BsonDocument("thread_id", threadId);
        return await repository.FindAllAsync(filter);
    }

    /// <summary>
    /// Every message the caller is a party to, sent or received.
    /// </summary>
    /// <remarks>
    /// Filtering on <c>recipient_email</c> alone made a conversation the caller STARTED invisible to them:
    /// the client groups this flat list into threads, so a thread with no received message had no rows to
    /// group and the inbox drew its "no messages yet" empty state over a live conversation until the other
    /// side replied. A thread has two participants and both of them own it.
    /// </remarks>
    public async Task<IEnumerable<MessageEntity>> GetInboxAsync(string participantEmail)
    {
        var filter = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("recipient_email", participantEmail),
            new BsonDocument("sender_email", participantEmail)
        });
        return await repository.FindAllAsync(filter);
    }

    public async Task MarkReadAsync(string messageId)
    {
        var message = await repository.GetByIdAsync(messageId);
        if (message is null) return;
        message.IsRead = true;
        await repository.UpdateAsync(messageId, message);
    }
}
