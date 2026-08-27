using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class NotificationsViewModelTests
{
    private static Mock<IUserSessionService> CreateMockSession(string email = "sarah.mitchell@agendabuddy.dev", string role = "Provider")
    {
        var session = new Mock<IUserSessionService>();
        session.Setup(s => s.Email).Returns(email);
        session.Setup(s => s.Role).Returns(role);
        session.Setup(s => s.IsProvider).Returns(role == "Provider");
        session.Setup(s => s.IsCustomer).Returns(role == "Customer");
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

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

        var vm = new NotificationsViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Notifications.Count);
        Assert.Equal(2, vm.UnreadCount);
        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    // A genuine zero-result success surfaces the empty state (IsEmpty), never
    // fabricated seed notifications.
    [Fact]
    public async Task LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData()
    {
        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<NotificationSummary>());

        var vm = new NotificationsViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Notifications);
        Assert.False(vm.HasError);
        Assert.True(vm.IsEmpty);
    }

    // A genuine failure surfaces the error banner (HasError + a real ErrorMessage),
    // never fabricated seed notifications.
    [Fact]
    public async Task LoadAsync_NetworkError_SetsHasErrorTrueWithRealMessage_NoFabricatedData()
    {
        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = new NotificationsViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Notifications);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    // ux-review.md finding 1 / PRD Requirement 12 / AC13: the exact empty-state copy — acknowledgement
    // + value prop, no error tone — on a genuinely empty list.
    [Fact]
    public async Task LoadAsync_EmptyResult_RendersExactEmptyStateCopy()
    {
        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<NotificationSummary>());

        var vm = new NotificationsViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsEmpty);
        Assert.Equal(
            "No notifications yet — you'll see updates about your appointments here.",
            vm.EmptyStateMessage);
    }

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

        var vm = new NotificationsViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.UnreadCount);

        await vm.MarkReadCommand.ExecuteAsync("n1");

        Assert.Equal(1, vm.UnreadCount);
        var item = vm.Notifications.First(n => n.Id == "n1");
        Assert.True(item.IsRead);
    }

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

        var vm = new NotificationsViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.UnreadCount);

        await vm.MarkReadCommand.ExecuteAsync("n1");

        Assert.Equal(1, vm.UnreadCount);
        Assert.False(vm.Notifications[0].IsRead);
    }
}
