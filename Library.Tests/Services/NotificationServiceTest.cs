using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Library.Entities;
using Library.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Library.Tests.Services;

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

        _repoMock.Setup(r => r.FindAllAsync(It.IsAny<BsonDocument>()))
            .ReturnsAsync(notifications);

        var result = await _svc.GetForRecipientAsync("customer@example.com");

        Assert.Equal(2, result.Count());
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

    [Fact]
    public void NotificationTypeEnum_HasExpectedValues()
    {
        Assert.Equal(0, (int)NotificationType.AppointmentBooked);
        Assert.Equal(1, (int)NotificationType.AppointmentUpdated);
        Assert.Equal(2, (int)NotificationType.AppointmentCancelled);
        Assert.Equal(3, (int)NotificationType.AppointmentCompleted);
    }
}
