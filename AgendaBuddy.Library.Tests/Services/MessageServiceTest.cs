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

        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(messages);

        var result = await _svc.GetInboxAsync("c@example.com");
        Assert.Equal(2, result.Count());
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
