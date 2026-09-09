using System.Linq;

namespace AgendaBuddy.Library.Services;

public class MessageService(IRepository<MessageEntity> repository) : IMessageService
{
    /// <summary>
    /// Oldest first. A conversation is read downwards, so this is the only order the thread view can render
    /// without sorting a full read in memory.
    /// </summary>
    /// <remarks>
    /// Without it the driver answers in natural order, which happens to be insertion order until something
    /// deletes a document — so a thread rendered "message 1, message 2, message 24, message 9, message 1".
    /// </remarks>
    private static readonly BsonDocument OldestFirst = new("sent_at", 1);

    /// <summary>
    /// The longest body the store will accept. Generous enough for a long explanation, bounded because the
    /// thread view renders every body as wrapped text in a 280pt bubble and nothing else caps it.
    /// </summary>
    public const int MaxBodyLength = 4000;

    public async Task SendMessageAsync(MessageEntity message)
    {
        message.Id = ObjectId.GenerateNewId();
        message.ThreadId = ThreadIdFor(message.SenderEmail, message.RecipientEmail);
        await repository.InsertAsync(message);
    }

    public async Task<IEnumerable<MessageEntity>> GetThreadAsync(string senderEmail, string recipientEmail)
    {
        var filter = new BsonDocument("thread_id", ThreadIdFor(senderEmail, recipientEmail));
        return await repository.FindAllAsync(filter, OldestFirst, int.MaxValue);
    }

    /// <summary>
    /// Every message the caller is a party to, sent or received.
    /// </summary>
    /// <remarks>
    /// Filtering on <c>recipient_email</c> alone made a conversation the caller STARTED invisible to them:
    /// the client groups this flat list into threads, so a thread with no received message had no rows to
    /// group and the inbox drew its "no messages yet" empty state over a live conversation until the other
    /// side replied. A thread has two participants and both of them own it.
    /// <para>
    /// ⚠️ Deliberately <b>not</b> bounded the way a notification inbox is. The client derives the thread list
    /// from this flat result, so capping it at the newest N messages would drop whole conversations from the
    /// list rather than trim each one — a provider with N recent messages in one thread would see only that
    /// thread. Bounding it needs a server-side per-thread summary instead, which is a different shape.
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<MessageEntity>> GetInboxAsync(string participantEmail)
    {
        var filter = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("recipient_email", participantEmail),
            new BsonDocument("sender_email", participantEmail)
        });
        return await repository.FindAllAsync(filter, OldestFirst, int.MaxValue);
    }

    /// <remarks>
    /// A targeted <c>$set</c> (ADR-032), not a read followed by <c>UpdateAsync</c> — which replaces the whole
    /// document and would let a concurrent write to any other field be lost to a read that predated it.
    /// <c>is_read: false</c> is part of the filter rather than a preceding check, so marking an already-read
    /// message writes nothing.
    /// </remarks>
    public async Task MarkReadAsync(string messageId)
    {
        if (!ObjectId.TryParse(messageId, out var objectId)) return;

        await repository.FindOneAndUpdateAsync(
            new BsonDocument { { "_id", objectId }, { "is_read", false } },
            new BsonDocument("$set", new BsonDocument("is_read", true)));
    }

    /// <summary>
    /// Marks every message the counterpart sent in this thread as read, and answers how many changed.
    /// </summary>
    /// <remarks>
    /// One multi-document <c>$set</c>, because opening a thread marks its whole unread set read and the
    /// client used to do that with one HTTP round trip — and one read-then-replace — per message. A thread
    /// with 250 unread messages meant 250 sequential requests behind a spinner.
    /// <para>
    /// <c>sender_email</c> is in the filter, so this only ever clears messages the caller RECEIVED: a sender
    /// marking their own message read is meaningless, and the thread id alone would cover both sides.
    /// </para>
    /// </remarks>
    public async Task<long> MarkThreadReadAsync(string callerEmail, string counterpartEmail) =>
        await repository.UpdateManyAsync(
            new BsonDocument
            {
                { "thread_id", ThreadIdFor(callerEmail, counterpartEmail) },
                { "recipient_email", callerEmail },
                { "is_read", false }
            },
            new BsonDocument("$set", new BsonDocument("is_read", true)));

    /// <summary>
    /// The id both participants derive for the conversation between them.
    /// </summary>
    /// <remarks>
    /// Sorted, so it is the same string whichever side asks — which is what makes a thread between two other
    /// people unrequestable rather than merely refused. Shared by the write and both reads so they cannot
    /// drift.
    /// </remarks>
    private static string ThreadIdFor(string oneEmail, string otherEmail)
    {
        var participants = new[] { oneEmail, otherEmail }
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return $"{participants[0]}::{participants[1]}";
    }
}
