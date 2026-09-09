using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace AgendaBuddy.Library.Tests.Services;

public class MessageServiceTest
{
    private readonly Mock<Library.Repositories.IRepository<MessageEntity>> _repoMock;
    private readonly MessageService _svc;

    public MessageServiceTest()
    {
        _repoMock = new Mock<Library.Repositories.IRepository<MessageEntity>>();
        _svc = new MessageService(_repoMock.Object);
    }

    [Fact]
    public async Task SendMessageAsync_GeneratesIdAndThreadId()
    {
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<MessageEntity>()))
            .Returns(Task.CompletedTask);

        var msg = new MessageEntity("provider@example.com", "customer@example.com", "Hello!");
        await _svc.SendMessageAsync(msg);

        Assert.NotEqual(ObjectId.Empty, msg.Id);
        Assert.False(string.IsNullOrWhiteSpace(msg.ThreadId));
    }

    [Fact]
    public async Task SendMessageAsync_ThreadId_IsAlphabeticallySorted()
    {
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<MessageEntity>()))
            .Returns(Task.CompletedTask);

        // "customer" < "provider" alphabetically
        var msg = new MessageEntity("provider@example.com", "customer@example.com", "Hi");
        await _svc.SendMessageAsync(msg);

        Assert.StartsWith("customer@example.com", msg.ThreadId);
        Assert.Contains("::", msg.ThreadId);
    }

    [Fact]
    public async Task SendMessageAsync_ThreadId_SameForBothDirections()
    {
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<MessageEntity>()))
            .Returns(Task.CompletedTask);

        var msg1 = new MessageEntity("provider@example.com", "customer@example.com", "Hi");
        var msg2 = new MessageEntity("customer@example.com", "provider@example.com", "Hey");

        await _svc.SendMessageAsync(msg1);
        await _svc.SendMessageAsync(msg2);

        Assert.Equal(msg1.ThreadId, msg2.ThreadId);
    }

    [Fact]
    public async Task GetInboxAsync_ReturnsMessagesForRecipient()
    {
        var messages = new List<MessageEntity>
        {
            new("p@example.com", "c@example.com", "Msg 1"),
            new("p@example.com", "c@example.com", "Msg 2"),
        };

        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .ReturnsAsync(messages);

        var result = await _svc.GetInboxAsync("c@example.com");
        Assert.Equal(2, result.Count());
    }

    // A thread has two participants and both own it. Filtering on recipient_email alone made a conversation
    // the caller STARTED invisible to them — the client groups this flat list into threads, so a thread with no
    // received message had no rows to group and the inbox drew "no messages yet" over a live conversation.
    [Fact]
    public async Task GetInboxAsync_MatchesEitherSideOfTheConversation()
    {
        BsonDocument? captured = null;
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .Callback<BsonDocument, BsonDocument, int>((filter, _, _) => captured = filter)
            .ReturnsAsync(new List<MessageEntity>());

        await _svc.GetInboxAsync("p@example.com");

        Assert.NotNull(captured);
        var clauses = captured!["$or"].AsBsonArray;
        Assert.Equal(2, clauses.Count);
        Assert.Contains(clauses, c => c.AsBsonDocument.Contains("recipient_email")
                                      && c.AsBsonDocument["recipient_email"] == "p@example.com");
        Assert.Contains(clauses, c => c.AsBsonDocument.Contains("sender_email")
                                      && c.AsBsonDocument["sender_email"] == "p@example.com");
    }

    // The driver answers in natural order without a sort, which is insertion order only until something
    // deletes a document — a conversation rendered "message 1, message 2, message 24, message 9, message 1".
    [Fact]
    public async Task GetThreadAsync_SortsOldestFirstInTheDatabase()
    {
        BsonDocument? sort = null;
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .Callback<BsonDocument, BsonDocument, int>((_, s, _) => sort = s)
            .ReturnsAsync(new List<MessageEntity>());

        await _svc.GetThreadAsync("p@example.com", "c@example.com");

        Assert.NotNull(sort);
        Assert.Equal(1, sort!["sent_at"].AsInt32);
    }

    [Fact]
    public async Task GetInboxAsync_SortsInTheDatabase()
    {
        BsonDocument? sort = null;
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .Callback<BsonDocument, BsonDocument, int>((_, s, _) => sort = s)
            .ReturnsAsync(new List<MessageEntity>());

        await _svc.GetInboxAsync("p@example.com");

        Assert.NotNull(sort);
        Assert.Equal(1, sort!["sent_at"].AsInt32);
    }

    [Fact]
    public async Task GetThreadAsync_DerivesTheSameThreadIdFromEitherSide()
    {
        var captured = new List<BsonDocument>();
        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .Callback<BsonDocument, BsonDocument, int>((f, _, _) => captured.Add(f))
            .ReturnsAsync(new List<MessageEntity>());

        await _svc.GetThreadAsync("p@example.com", "c@example.com");
        await _svc.GetThreadAsync("c@example.com", "p@example.com");

        Assert.Equal(captured[0]["thread_id"], captured[1]["thread_id"]);
    }

    // ADR-032: a targeted $set, not a read followed by a whole-document replace, and `is_read: false` lives in
    // the FILTER so marking an already-read message writes nothing.
    [Fact]
    public async Task MarkReadAsync_IsATargetedSetGuardedOnStillBeingUnread()
    {
        BsonDocument? filter = null;
        BsonDocument? update = null;
        _repoMock.Setup(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((f, u) => { filter = f; update = u; })
            .ReturnsAsync((MessageEntity?)null);

        var id = ObjectId.GenerateNewId();
        await _svc.MarkReadAsync(id.ToString());

        Assert.NotNull(filter);
        Assert.Equal(id, filter!["_id"].AsObjectId);
        Assert.False(filter["is_read"].AsBoolean);
        Assert.True(update!["$set"]["is_read"].AsBoolean);

        // Never the whole-document replace this used to be.
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<MessageEntity>()), Times.Never);
        _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_IgnoresAnUnparseableId()
    {
        await _svc.MarkReadAsync("not-an-object-id");

        _repoMock.Verify(
            r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()), Times.Never);
    }

    // One write for the whole thread. The client used to issue a request per unread message, all awaited before
    // the spinner cleared.
    [Fact]
    public async Task MarkThreadReadAsync_IsOneUpdateScopedToMessagesTheCallerReceived()
    {
        BsonDocument? filter = null;
        BsonDocument? update = null;
        _repoMock.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((f, u) => { filter = f; update = u; })
            .ReturnsAsync(7);

        var marked = await _svc.MarkThreadReadAsync("p@example.com", "c@example.com");

        Assert.Equal(7, marked);
        Assert.NotNull(filter);
        Assert.Equal("c@example.com::p@example.com", filter!["thread_id"].AsString);
        // Scoped to what the CALLER received: a sender marking their own message read is meaningless, and the
        // thread id alone would cover both sides of the conversation.
        Assert.Equal("p@example.com", filter["recipient_email"].AsString);
        Assert.False(filter["is_read"].AsBoolean);
        Assert.True(update!["$set"]["is_read"].AsBoolean);
    }

    [Fact]
    public void MaxBodyLength_IsBounded()
    {
        // Nothing else caps a body — the store accepted 62,000 characters, rendered as wrapped text in a
        // 280pt bubble.
        Assert.InRange(MessageService.MaxBodyLength, 1, 100_000);
    }

    [Fact]
    public void MessageEntity_DefaultIsRead_IsFalse()
    {
        var m = new MessageEntity();
        Assert.False(m.IsRead);
    }

    [Fact]
    public void MessageEntity_Constructor_SetsFields()
    {
        var m = new MessageEntity("a@b.com", "c@d.com", "Hello");
        Assert.Equal("a@b.com", m.SenderEmail);
        Assert.Equal("c@d.com", m.RecipientEmail);
        Assert.Equal("Hello", m.Body);
    }
}
