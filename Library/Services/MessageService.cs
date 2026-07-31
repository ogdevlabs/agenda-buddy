using System.Linq;

namespace Library.Services;

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

    public async Task<IEnumerable<MessageEntity>> GetInboxAsync(string recipientEmail)
    {
        var filter = new BsonDocument("recipient_email", recipientEmail);
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
