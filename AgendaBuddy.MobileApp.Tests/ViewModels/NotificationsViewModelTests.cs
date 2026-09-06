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
        Mock<INotificationApiService> service,
        out NotificationBadgeViewModel badge,
        IInAppAlertService? alerts = null)
    {
        badge = new NotificationBadgeViewModel(service.Object);
        return new NotificationsViewModel(service.Object, CreateMockSession().Object, badge, alerts);
    }

    /// <summary>Captures what the screen told the user, instead of drawing a toast there is no presenter for.</summary>
    private sealed class RecordingAlertService : IInAppAlertService
    {
        public List<string> Messages { get; } = new();

        public Task ShowAsync(string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowAsync(string message, string actionLabel, Func<Task> action)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
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

    /// <summary>
    /// A bulk action that reports nothing is indistinguishable from one that did nothing. This used to be
    /// silent on success, on refusal and on a dropped connection alike — the only feedback was a number that
    /// sometimes changed.
    /// </summary>
    [Fact]
    public async Task MarkAllRead_Success_ReportsHowManyItMarked()
    {
        var service = CreateService(ThreeRows());
        service.Setup(s => s.MarkAllReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var alerts = new RecordingAlertService();
        var vm = CreateViewModel(service, out _, alerts);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MarkAllReadCommand.ExecuteAsync(null);

        Assert.Equal("2 notifications marked as read", Assert.Single(alerts.Messages));
        Assert.False(vm.HasError);
    }

    // "1 notification", not "1 notifications".
    [Fact]
    public async Task MarkAllRead_OfOne_ReportsItInTheSingular()
    {
        Assert.Equal("1 notification marked as read", NotificationsViewModel.MarkAllReadConfirmation(1));
        await Task.CompletedTask;
    }

    /// <summary>
    /// A failed request must change nothing locally: clearing the rows would show a state the next reload
    /// contradicts, so the reader would believe the inbox was cleared when it was not.
    /// </summary>
    [Fact]
    public async Task MarkAllRead_WhenTheRequestFails_SaysSoAndLeavesEveryRowUnread()
    {
        var service = CreateService(ThreeRows());
        service.Setup(s => s.MarkAllReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);

        var vm = CreateViewModel(service, out var badge);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MarkAllReadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal(NotificationsViewModel.MarkAllReadFailureMessage, vm.ErrorMessage);
        Assert.Equal(2, vm.UnreadCount);
        Assert.Equal(2, badge.UnreadCount);
        Assert.False(vm.Notifications.First(n => n.Id == "n1").IsRead);
    }

    // A thrown request is the same outcome as a refused one, and must not escape the command unobserved.
    [Fact]
    public async Task MarkAllRead_WhenTheRequestThrows_SaysSoRatherThanFailingSilently()
    {
        var service = CreateService(ThreeRows());
        service.Setup(s => s.MarkAllReadAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network error"));

        var vm = CreateViewModel(service, out _);
        await vm.LoadCommand.ExecuteAsync(null);

        var ex = await Record.ExceptionAsync(() => vm.MarkAllReadCommand.ExecuteAsync(null));

        Assert.Null(ex);
        Assert.Equal(NotificationsViewModel.MarkAllReadFailureMessage, vm.ErrorMessage);
        Assert.Equal(2, vm.UnreadCount);
    }

    /// <summary>
    /// The server having nothing unread is not a failure — it means this client's count was stale (read on
    /// another device, most likely). Reconcile and say so, rather than reporting an error that did not happen.
    /// </summary>
    [Fact]
    public async Task MarkAllRead_WhenTheServerHadNothingUnread_ReconcilesInsteadOfReportingAFailure()
    {
        var service = CreateService(ThreeRows(), unreadCount: 2);
        service.Setup(s => s.MarkAllReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0L);

        var alerts = new RecordingAlertService();
        var vm = CreateViewModel(service, out _, alerts);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.UnreadCount);

        // What the server now reports, after the no-op.
        service.Setup(s => s.GetUnreadCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0L);

        await vm.MarkAllReadCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Equal(0, vm.UnreadCount);
        Assert.Equal(NotificationsViewModel.NothingToMarkMessage, Assert.Single(alerts.Messages));
    }

    // ── Pull to refresh ─────────────────────────────────────────────────────────────────────────────
    // The gesture people try first on any list they suspect is stale. There was none: the only way to re-read
    // the inbox was to navigate away and back so OnAppearing fired.

    [Fact]
    public async Task Refresh_ReloadsAndClearsItsOwnSpinner()
    {
        var service = CreateService(ThreeRows());
        var vm = CreateViewModel(service, out _);

        await vm.RefreshCommand.ExecuteAsync(null);

        service.Verify(s => s.GetNotificationsAsync(
            NotificationsViewModel.PageSize, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(vm.IsRefreshing);
        Assert.Equal(3, vm.Notifications.Count);
    }

    /// <summary>
    /// A pull-to-refresh draws its own spinner, so the centred one must stay hidden — two spinners at once read
    /// as two separate things loading.
    /// </summary>
    [Fact]
    public void TheCentredSpinnerIsSuppressedWhileTheRefreshGestureOwnsTheScreen()
    {
        var vm = CreateViewModel(CreateService(), out _);

        vm.IsLoading = true;
        Assert.True(vm.ShowsLoadingIndicator);

        vm.IsRefreshing = true;
        Assert.False(vm.ShowsLoadingIndicator);
    }

    // ── Date banding ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The header is stamped on the first row of each band and cleared on the rest, so a flat list can draw
    /// "Today"/"Yesterday" headers without a grouped CollectionView.
    /// </summary>
    [Fact]
    public async Task LoadAsync_StampsADateHeaderOnTheFirstRowOfEachBandOnly()
    {
        var now = DateTime.UtcNow;
        var rows = new List<NotificationSummary>
        {
            new() { Id = "a", CreatedAt = now.AddHours(-1) },
            new() { Id = "b", CreatedAt = now.AddHours(-2) },
            new() { Id = "c", CreatedAt = now.AddDays(-1) },
            new() { Id = "d", CreatedAt = now.AddDays(-1).AddHours(-1) }
        };

        var vm = CreateViewModel(CreateService(rows), out _);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Today", vm.Notifications[0].SectionHeader);
        Assert.True(vm.Notifications[0].StartsSection);

        Assert.Equal(string.Empty, vm.Notifications[1].SectionHeader);
        Assert.False(vm.Notifications[1].StartsSection);

        Assert.Equal("Yesterday", vm.Notifications[2].SectionHeader);
        Assert.Equal(string.Empty, vm.Notifications[3].SectionHeader);
    }

    /// <summary>
    /// Banded on <b>local</b> dates. The server stores UTC instants, and banding on those files a 01:00Z
    /// notification under the wrong day for every reader behind UTC — the same defect ProviderAvailability had.
    /// </summary>
    [Fact]
    public void TheBandingUsesTheReadersOwnClock()
    {
        var utc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var rows = new List<NotificationSummary> { new() { Id = "a", CreatedAt = utc } };

        NotificationsViewModel.ApplySections(rows);

        Assert.Equal(
            AgendaBuddy.MobileApp.Infrastructure.NotificationVisuals.Section(utc.ToLocalTime(), DateTime.Now),
            rows[0].SectionHeader);
    }

    // ── Row chrome ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Unread is what the list has to make obvious, so it is a property the row's own chrome binds to rather
    /// than IsRead inverted at four separate places in the template.
    /// </summary>
    [Fact]
    public void MarkingARowReadFlipsWhatTheRowChromeBindsTo()
    {
        var row = new NotificationSummary { Id = "n1", IsRead = false };
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.True(row.IsUnread);
        row.IsRead = true;

        Assert.False(row.IsUnread);
        Assert.Contains(nameof(NotificationSummary.IsUnread), raised);
    }

    /// <summary>
    /// The expanded row's timestamp is on the reader's clock. It used to format the raw UTC instant, so it read
    /// hours off for anyone not on UTC while the "3h ago" line directly above it was right.
    /// </summary>
    [Fact]
    public void TheExpandedTimestampIsLocalNotUtc()
    {
        var utc = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        var row = new NotificationSummary { CreatedAt = utc };

        Assert.Equal(utc.ToLocalTime(), row.LocalCreatedAt);
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
