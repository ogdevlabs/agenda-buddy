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

    /// <summary>
    /// Wires the mock so the unread count comes from <c>unread-count</c>, which is where the view model reads
    /// it — a page can be filtered or limited, so counting the returned list would under-report.
    /// </summary>
    private static Mock<INotificationApiService> CreateService(
        List<NotificationSummary>? page = null, long? unreadCount = null)
    {
        var rows = page ?? [];
        var service = new Mock<INotificationApiService>();
        service.Setup(s => s.GetNotificationsAsync(
                   It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(rows);
        service.Setup(s => s.GetUnreadCountAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(unreadCount ?? rows.Count(n => !n.IsRead));
        return service;
    }

    private static NotificationsViewModel CreateViewModel(
        Mock<INotificationApiService> service, out NotificationBadgeViewModel badge)
    {
        badge = new NotificationBadgeViewModel(service.Object);
        return new NotificationsViewModel(service.Object, CreateMockSession().Object, badge);
    }

    private static List<NotificationSummary> ThreeRows() =>
    [
        new() { Id = "n1", Type = NotificationType.AppointmentRequested, Subject = "New appointment request", Body = "Requested", IsRead = false },
        new() { Id = "n2", Type = NotificationType.AppointmentUpdated, Subject = "Appointment confirmed", Body = "Updated", IsRead = true },
        new() { Id = "n3", Type = NotificationType.AppointmentCancelled, Subject = "Appointment cancelled", Body = "Cancelled", IsRead = false }
    ];

    [Fact]
    public async Task LoadAsync_Success_SetsNotificationsAndUnreadCount()
    {
        var service = CreateService(ThreeRows());
        var vm = CreateViewModel(service, out _);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Notifications.Count);
        Assert.Equal(2, vm.UnreadCount);
        Assert.True(vm.HasUnread);
        Assert.Equal(string.Empty, vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    /// <summary>
    /// The badge is a singleton shared with MorePage, so loading this screen has to update the number the
    /// other surface shows — not just this view model's own copy.
    /// </summary>
    [Fact]
    public async Task LoadAsync_RefreshesTheSharedBadge()
    {
        var service = CreateService(ThreeRows(), unreadCount: 2);
        var vm = CreateViewModel(service, out var badge);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, badge.UnreadCount);
        Assert.True(badge.HasUnread);
    }

    // A genuine zero-result success surfaces the empty state (IsEmpty), never
    // fabricated seed notifications.
    [Fact]
    public async Task LoadAsync_EmptyResult_SetsIsEmptyTrue_NoFabricatedData()
    {
        var vm = CreateViewModel(CreateService(), out _);

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
        service.Setup(s => s.GetNotificationsAsync(
                   It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = CreateViewModel(service, out _);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Notifications);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        // Not the empty state: "you have no notifications" and "we could not read your notifications" are
        // different statements, and only one of them is true here.
        Assert.False(vm.IsEmpty);
    }

    // The empty-state copy — acknowledgement + value prop, no error tone — on a genuinely empty list.
    [Fact]
    public async Task LoadAsync_EmptyResult_RendersExactEmptyStateCopy()
    {
        var vm = CreateViewModel(CreateService(), out _);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsEmpty);
        Assert.Equal(
            "No notifications yet — you'll see updates about your appointments here.",
            vm.EmptyStateMessage);
    }

    /// <summary>
    /// A filtered-empty inbox is not an empty inbox, and saying "no notifications yet" to somebody who has
    /// simply read all of theirs is wrong.
    /// </summary>
    [Fact]
    public async Task EmptyStateCopy_DistinguishesFilteredEmptyFromEmpty()
    {
        var vm = CreateViewModel(CreateService(), out _);
        await vm.ToggleUnreadFilterCommand.ExecuteAsync(null);

        Assert.True(vm.ShowUnreadOnly);
        Assert.Equal("Nothing unread — you're all caught up.", vm.EmptyStateMessage);
    }

    // ── Mark read ───────────────────────────────────────────────────────────────────────────────────
    // The whole point: it has to reach the server. Local-only state was silently discarded by the next
    // reload, so nothing in the app ever wrote is_read.

    [Fact]
    public async Task MarkReadAsync_CallsTheServer_ThenUpdatesItemAndDecrementsUnreadCount()
    {
        var rows = ThreeRows();
        var service = CreateService(rows);
        service.Setup(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = CreateViewModel(service, out var badge);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.UnreadCount);

        await vm.MarkReadCommand.ExecuteAsync(vm.Notifications.First(n => n.Id == "n1"));

        service.Verify(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, vm.UnreadCount);
        Assert.Equal(1, badge.UnreadCount);
        Assert.True(vm.Notifications.First(n => n.Id == "n1").IsRead);
    }

    [Fact]
    public async Task MarkReadAsync_ServerRefuses_LeavesTheRowUnread()
    {
        var rows = ThreeRows();
        var service = CreateService(rows);
        service.Setup(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var vm = CreateViewModel(service, out var badge);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MarkReadCommand.ExecuteAsync(vm.Notifications.First(n => n.Id == "n1"));

        // Unchanged, so the count cannot drift from what the next reload reports.
        Assert.Equal(2, vm.UnreadCount);
        Assert.Equal(2, badge.UnreadCount);
        Assert.False(vm.Notifications.First(n => n.Id == "n1").IsRead);
    }

    [Fact]
    public async Task MarkReadAsync_AlreadyRead_DoesNotCallTheServer()
    {
        var service = CreateService(ThreeRows());
        var vm = CreateViewModel(service, out _);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MarkReadCommand.ExecuteAsync(vm.Notifications.First(n => n.Id == "n2"));

        service.Verify(s => s.MarkReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Expanding a row is what marks it read, so the read has to travel — this asserts the timer path calls
    /// the API and not only the local copy.
    /// </summary>
    [Fact]
    public async Task ScheduleMarkRead_OnAnExpandedRow_MarksItReadOnTheServer()
    {
        var service = CreateService(ThreeRows());
        service.Setup(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = CreateViewModel(service, out _);
        await vm.LoadCommand.ExecuteAsync(null);

        var row = vm.Notifications.First(n => n.Id == "n1");
        row.IsExpanded = true;
        await vm.ScheduleMarkReadAsync(row);

        service.Verify(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(row.IsRead);
    }

    [Fact]
    public async Task ScheduleMarkRead_CollapsedAgainBeforeTheTimerElapses_DoesNotMarkRead()
    {
        var service = CreateService(ThreeRows());
        var vm = CreateViewModel(service, out _);
        await vm.LoadCommand.ExecuteAsync(null);

        var row = vm.Notifications.First(n => n.Id == "n1");
        row.IsExpanded = false;
        await vm.ScheduleMarkReadAsync(row);

        service.Verify(s => s.MarkReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(row.IsRead);
    }

    // ── Mark all read ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllRead_ClearsEveryRowAndTheCount()
    {
        var service = CreateService(ThreeRows());
        service.Setup(s => s.MarkAllReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var vm = CreateViewModel(service, out var badge);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.CanMarkAllRead);

        await vm.MarkAllReadCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.UnreadCount);
        Assert.Equal(0, badge.UnreadCount);
        Assert.All(vm.Notifications, n => Assert.True(n.IsRead));
        Assert.False(vm.CanMarkAllRead);
    }

    [Fact]
    public async Task MarkAllRead_WithNothingUnread_DoesNotCallTheServer()
    {
        var service = CreateService([
            new() { Id = "n2", Type = NotificationType.AppointmentUpdated, Subject = "Confirmed", IsRead = true }
        ]);

        var vm = CreateViewModel(service, out _);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanMarkAllRead);
        await vm.MarkAllReadCommand.ExecuteAsync(null);

        service.Verify(s => s.MarkAllReadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Filter ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleUnreadFilter_ReloadsWithUnreadOnly()
    {
        var service = CreateService(ThreeRows());
        var vm = CreateViewModel(service, out _);

        await vm.ToggleUnreadFilterCommand.ExecuteAsync(null);

        Assert.True(vm.ShowUnreadOnly);
        Assert.Equal("Show all", vm.UnreadFilterLabel);
        service.Verify(s => s.GetNotificationsAsync(It.IsAny<int?>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Navigation ──────────────────────────────────────────────────────────────────────────────────
    // A notification carrying an appointment identifier must be able to open it. Before this, tapping one
    // only expanded the card and the identifier the producer stored was never used for anything.

    [Fact]
    public async Task ViewAppointment_NavigatesToTheAppointmentAndMarksTheRowRead()
    {
        var rows = new List<NotificationSummary>
        {
            new()
            {
                Id = "n1",
                Type = NotificationType.AppointmentRequested,
                Subject = "New appointment request",
                AppointmentIdentifier = "appt-42",
                IsRead = false
            }
        };
        var service = CreateService(rows);
        service.Setup(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = new RecordingNotificationsViewModel(
            service.Object, CreateMockSession().Object, new NotificationBadgeViewModel(service.Object));
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ViewAppointmentCommand.ExecuteAsync(vm.Notifications[0]);

        Assert.Equal("appt-42", vm.NavigatedTo);
        Assert.True(vm.Notifications[0].IsRead);
    }

    [Fact]
    public async Task ViewAppointment_OnANotificationWithNoAppointment_DoesNothing()
    {
        var rows = new List<NotificationSummary>
        {
            new()
            {
                Id = "n1",
                Type = NotificationType.MessageReceived,
                Subject = "New message from a@b.dev",
                AppointmentIdentifier = string.Empty,
                IsRead = false
            }
        };
        var service = CreateService(rows);

        var vm = new RecordingNotificationsViewModel(
            service.Object, CreateMockSession().Object, new NotificationBadgeViewModel(service.Object));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.Notifications[0].HasAppointment);
        await vm.ViewAppointmentCommand.ExecuteAsync(vm.Notifications[0]);

        Assert.Null(vm.NavigatedTo);
        service.Verify(s => s.MarkReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A cancellation CAN be opened, now that cancelling is a soft delete — the appointment survives with
    /// <c>Cancelled</c> status, so the detail page can fetch it and show what was called off. While cancelling
    /// hard-deleted the document this was suppressed, because the button led to a page that could fetch nothing.
    /// </summary>
    [Fact]
    public async Task ViewAppointment_OnACancellation_OpensTheCancelledAppointment()
    {
        var rows = new List<NotificationSummary>
        {
            new()
            {
                Id = "n1",
                Type = NotificationType.AppointmentCancelled,
                Subject = "Appointment cancelled",
                AppointmentIdentifier = "appt-42",
                IsRead = false
            }
        };
        var service = CreateService(rows);
        service.Setup(s => s.MarkReadAsync("n1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var vm = new RecordingNotificationsViewModel(
            service.Object, CreateMockSession().Object, new NotificationBadgeViewModel(service.Object));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.Notifications[0].CanOpenAppointment);

        await vm.ViewAppointmentCommand.ExecuteAsync(vm.Notifications[0]);

        Assert.Equal("appt-42", vm.NavigatedTo);
    }

    /// <summary>
    /// Every appointment notification can be opened. Kept as a property distinct from
    /// <c>HasAppointment</c> because "names an appointment" and "can be opened" are different questions, and the
    /// next state that cannot be opened belongs there rather than in the view.
    /// </summary>
    [Theory]
    [InlineData(NotificationType.AppointmentRequested)]
    [InlineData(NotificationType.AppointmentBooked)]
    [InlineData(NotificationType.AppointmentUpdated)]
    [InlineData(NotificationType.AppointmentCompleted)]
    [InlineData(NotificationType.AppointmentCancelled)]
    public void CanOpenAppointment_IsTrueForEveryAppointmentNotification(NotificationType type)
    {
        Assert.True(new NotificationSummary { Type = type, AppointmentIdentifier = "appt-1" }.CanOpenAppointment);
    }

    // A message notification carries no identifier, so there is nothing to open.
    [Fact]
    public void CanOpenAppointment_IsFalseWithoutAnIdentifier()
    {
        Assert.False(new NotificationSummary
        {
            Type = NotificationType.MessageReceived,
            AppointmentIdentifier = string.Empty
        }.CanOpenAppointment);
    }

    /// <summary>
    /// The page size is sent explicitly. An unstated one is a coupling to a server constant the client cannot
    /// see — the same invisible agreement that let the wire contract and the client model drift apart.
    /// </summary>
    [Fact]
    public async Task LoadAsync_SendsAnExplicitPageSize()
    {
        var service = CreateService(ThreeRows());
        var vm = CreateViewModel(service, out _);

        await vm.LoadCommand.ExecuteAsync(null);

        service.Verify(s => s.GetNotificationsAsync(
            NotificationsViewModel.PageSize, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Captures the navigation instead of performing it — <c>Shell.Current</c> does not exist on the
    /// <c>net10.0</c> test slice.
    /// </summary>
    private sealed class RecordingNotificationsViewModel(
        INotificationApiService notificationApiService,
        IUserSessionService session,
        NotificationBadgeViewModel badge)
        : NotificationsViewModel(notificationApiService, session, badge)
    {
        public string? NavigatedTo { get; private set; }

        protected override Task NavigateToAppointmentAsync(string appointmentIdentifier)
        {
            NavigatedTo = appointmentIdentifier;
            return Task.CompletedTask;
        }
    }
}
