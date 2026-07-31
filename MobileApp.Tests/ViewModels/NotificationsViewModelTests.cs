using Library.Entities;
using MobileApp.Models;
using MobileApp.Services;
using MobileApp.ViewModels;
using Moq;
using Xunit;

namespace MobileApp.Tests.ViewModels;

public class NotificationsViewModelTests
{
    // ---------------------------------------------------------------------------
    // LoadAsync — success path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_Success_SetsNotificationsAndUnreadCount()
    {
        var notifications = new List<NotificationSummary>
        {
            new() { Id = "n1", NotificationType = NotificationType.AppointmentBooked, Message = "Booked", IsRead = false },
            new() { Id = "n2", NotificationType = NotificationType.AppointmentUpdated, Message = "Updated", IsRead = true },
            new() { Id = "n3", NotificationType = NotificationType.AppointmentCancelled, Message = "Cancelled", IsRead = false }
        };

        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(notifications);

        var vm = new NotificationsViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Notifications.Count);
        Assert.Equal(2, vm.UnreadCount);
        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    // ---------------------------------------------------------------------------
    // LoadAsync — empty result
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_EmptyResult_IsEmptyIsTrue()
    {
        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<NotificationSummary>());

        var vm = new NotificationsViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Notifications);
        Assert.Equal(0, vm.UnreadCount);
        Assert.False(vm.HasError);
        Assert.True(vm.IsEmpty);
    }

    // ---------------------------------------------------------------------------
    // LoadAsync — network error
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_NetworkError_SetsErrorMessage()
    {
        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = new NotificationsViewModel(service.Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal(
            "Could not load notifications — check your connection and try again.",
            vm.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // MarkReadAsync — updates item and decrements unread count (PRD AC-12)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MarkReadAsync_UpdatesItemAndDecrementsUnreadCount()
    {
        var notifications = new List<NotificationSummary>
        {
            new() { Id = "n1", NotificationType = NotificationType.AppointmentBooked, Message = "Booked", IsRead = false },
            new() { Id = "n2", NotificationType = NotificationType.AppointmentUpdated, Message = "Updated", IsRead = false }
        };

        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(notifications);
        service.Setup(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new NotificationSummary { Id = "n1", IsRead = true });

        var vm = new NotificationsViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.UnreadCount);

        await vm.MarkReadCommand.ExecuteAsync("n1");

        Assert.Equal(1, vm.UnreadCount);
        var item = vm.Notifications.First(n => n.Id == "n1");
        Assert.True(item.IsRead);
    }

    // ---------------------------------------------------------------------------
    // MarkReadAsync — no-op when service returns null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MarkReadAsync_ServiceReturnsNull_DoesNotChangeUnreadCount()
    {
        var notifications = new List<NotificationSummary>
        {
            new() { Id = "n1", NotificationType = NotificationType.AppointmentBooked, Message = "Booked", IsRead = false }
        };

        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(notifications);
        service.Setup(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((NotificationSummary?)null);

        var vm = new NotificationsViewModel(service.Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.UnreadCount);

        await vm.MarkReadCommand.ExecuteAsync("n1");

        Assert.Equal(1, vm.UnreadCount);
        Assert.False(vm.Notifications[0].IsRead);
    }
}
