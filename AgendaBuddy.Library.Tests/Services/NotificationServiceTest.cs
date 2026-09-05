using System;
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

public class NotificationServiceTest
{
    private readonly Mock<Library.Repositories.IRepository<NotificationEntity>> _repoMock;
    private readonly NotificationService _svc;

    public NotificationServiceTest()
    {
        _repoMock = new Mock<Library.Repositories.IRepository<NotificationEntity>>();
        _svc = new NotificationService(_repoMock.Object);
    }

    [Fact]
    public async Task SendAsync_InsertsNotificationWithGeneratedId()
    {
        var notification = new NotificationEntity(
            "provider@example.com",
            "Appointment Booked",
            "Your appointment has been booked.",
            NotificationType.AppointmentBooked,
            "appt-001");

        _repoMock.Setup(r => r.InsertAsync(It.IsAny<NotificationEntity>()))
            .Returns(Task.CompletedTask);

        await _svc.SendAsync(notification);

        _repoMock.Verify(r => r.InsertAsync(It.Is<NotificationEntity>(
            n => n.RecipientEmail == "provider@example.com"
                 && n.Type == NotificationType.AppointmentBooked)), Times.Once);
        Assert.NotEqual(ObjectId.Empty, notification.Id);
    }

    [Fact]
    public async Task GetForRecipientAsync_ReturnsMatchingNotifications()
    {
        var notifications = new List<NotificationEntity>
        {
            new("customer@example.com", "Booked", "", NotificationType.AppointmentBooked, "a1"),
            new("customer@example.com", "Cancelled", "", NotificationType.AppointmentCancelled, "a2"),
        };

        _repoMock.Setup(r => r.FindAllAsync(
                It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .ReturnsAsync(notifications);

        var result = await _svc.GetForRecipientAsync("customer@example.com");

        Assert.Equal(2, result.Count());
    }

    /// <summary>
    /// Newest first, scoped to the recipient, and bounded — all three in the database. Without the sort the
    /// driver answers in natural order, which is newest-first only until something deletes a document; without
    /// the limit an inbox read grows with the age of the account.
    /// </summary>
    [Fact]
    public async Task GetForRecipientAsync_ReadsNewestFirstAndBounded()
    {
        BsonDocument? filter = null;
        BsonDocument? sort = null;
        var limit = 0;

        _repoMock.Setup(r => r.FindAllAsync(
                It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .Callback<BsonDocument, BsonDocument, int>((f, s, l) => (filter, sort, limit) = (f, s, l))
            .ReturnsAsync([]);

        await _svc.GetForRecipientAsync("customer@example.com");

        Assert.Equal("customer@example.com", filter!["recipient_email"].AsString);
        Assert.False(filter.Contains("is_read"));
        Assert.Equal(-1, sort!["created_at"].AsInt32);
        Assert.Equal(NotificationService.DefaultPageSize, limit);
    }

    [Fact]
    public async Task GetForRecipientAsync_UnreadOnly_ExcludesReadRowsInTheDatabase()
    {
        BsonDocument? filter = null;

        _repoMock.Setup(r => r.FindAllAsync(
                It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .Callback<BsonDocument, BsonDocument, int>((f, _, _) => filter = f)
            .ReturnsAsync([]);

        await _svc.GetForRecipientAsync("customer@example.com", unreadOnly: true);

        Assert.False(filter!["is_read"].AsBoolean);
    }

    /// <summary>
    /// The cap is applied in the service as well as at the endpoint, so an in-process caller cannot ask for an
    /// unbounded read either.
    /// </summary>
    [Theory]
    [InlineData(0, NotificationService.DefaultPageSize)]
    [InlineData(-5, NotificationService.DefaultPageSize)]
    [InlineData(10, 10)]
    [InlineData(100_000, NotificationService.MaxPageSize)]
    public async Task GetForRecipientAsync_ClampsTheLimit(int requested, int expected)
    {
        var limit = 0;

        _repoMock.Setup(r => r.FindAllAsync(
                It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>(), It.IsAny<int>()))
            .Callback<BsonDocument, BsonDocument, int>((_, _, l) => limit = l)
            .ReturnsAsync([]);

        await _svc.GetForRecipientAsync("customer@example.com", requested);

        Assert.Equal(expected, limit);
    }

    /// <summary>
    /// A targeted <c>$set</c> (ADR-032), not a read followed by a whole-document replacement — and
    /// <c>is_read: false</c> lives in the filter, so marking an already-read notification writes nothing.
    /// </summary>
    [Fact]
    public async Task MarkReadAsync_SetsTheFlagWithoutReplacingTheDocument()
    {
        var id = ObjectId.GenerateNewId();
        BsonDocument? filter = null;
        BsonDocument? update = null;

        _repoMock.Setup(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((f, u) => (filter, update) = (f, u))
            .ReturnsAsync((NotificationEntity?)null);

        await _svc.MarkReadAsync(id.ToString());

        Assert.Equal(id, filter!["_id"].AsObjectId);
        Assert.False(filter["is_read"].AsBoolean);
        Assert.True(update!["$set"]["is_read"].AsBoolean);

        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<NotificationEntity>()), Times.Never);
    }

    // An unparseable id is a client bug, not a reason to throw a FormatException out of a route.
    [Fact]
    public async Task MarkReadAsync_MalformedId_WritesNothing()
    {
        await _svc.MarkReadAsync("not-an-object-id");

        _repoMock.Verify(r => r.FindOneAndUpdateAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()), Times.Never);
    }

    /// <summary>
    /// One multi-document write, scoped by recipient — not N read-modify-writes.
    /// </summary>
    [Fact]
    public async Task MarkAllReadAsync_UpdatesEveryUnreadRowForTheRecipientInOneWrite()
    {
        BsonDocument? filter = null;
        BsonDocument? update = null;

        _repoMock.Setup(r => r.UpdateManyAsync(It.IsAny<BsonDocument>(), It.IsAny<BsonDocument>()))
            .Callback<BsonDocument, BsonDocument>((f, u) => (filter, update) = (f, u))
            .ReturnsAsync(3);

        var marked = await _svc.MarkAllReadAsync("customer@example.com");

        Assert.Equal(3, marked);
        Assert.Equal("customer@example.com", filter!["recipient_email"].AsString);
        Assert.False(filter["is_read"].AsBoolean);
        Assert.True(update!["$set"]["is_read"].AsBoolean);
    }

    // Counted in the database. GetPagedAsync's TotalCount is the count of all matches, not the page size,
    // which is what makes a page of one the cheapest count available on this interface.
    [Fact]
    public async Task CountUnreadAsync_CountsWithoutReadingTheRows()
    {
        BsonDocument? filter = null;

        _repoMock.Setup(r => r.GetPagedAsync(It.IsAny<BsonDocument>(), 0, 1))
            .Callback<BsonDocument, int, int>((f, _, _) => filter = f)
            .ReturnsAsync((new List<NotificationEntity>(), 12L));

        Assert.Equal(12, await _svc.CountUnreadAsync("customer@example.com"));
        Assert.Equal("customer@example.com", filter!["recipient_email"].AsString);
        Assert.False(filter["is_read"].AsBoolean);
    }

    [Fact]
    public void NotificationEntity_DefaultIsRead_IsFalse()
    {
        var n = new NotificationEntity();
        Assert.False(n.IsRead);
    }

    [Fact]
    public void NotificationEntity_Constructor_SetsAllFields()
    {
        var n = new NotificationEntity(
            "r@example.com", "Subject", "Body",
            NotificationType.AppointmentUpdated, "appt-999");

        Assert.Equal("r@example.com", n.RecipientEmail);
        Assert.Equal("Subject", n.Subject);
        Assert.Equal(NotificationType.AppointmentUpdated, n.Type);
        Assert.Equal("appt-999", n.AppointmentIdentifier);
    }

    /// <summary>
    /// The integer of every member, because the integer is what is persisted: inserting or reordering anything
    /// silently reinterprets every notification already stored.
    /// </summary>
    [Fact]
    public void NotificationTypeEnum_HasExpectedValues()
    {
        Assert.Equal(0, (int)NotificationType.AppointmentBooked);
        Assert.Equal(1, (int)NotificationType.AppointmentUpdated);
        Assert.Equal(2, (int)NotificationType.AppointmentCancelled);
        Assert.Equal(3, (int)NotificationType.AppointmentCompleted);
        Assert.Equal(4, (int)NotificationType.PasswordResetRequested);
        Assert.Equal(5, (int)NotificationType.EmailConfirmationRequested);
        Assert.Equal(6, (int)NotificationType.AppointmentRequested);
        Assert.Equal(7, (int)NotificationType.MessageReceived);

        // A new member appended here has to be given a display label in NotificationSummary.TypeLabel, or it
        // renders as "Info" — which is how a booking request came to be labelled "Info".
        Assert.Equal(8, Enum.GetValues<NotificationType>().Length);
    }
}
