using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Dtos;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

public class CalendarViewModelTests
{
    private static AppointmentDetail Appointment(
        string id,
        DateTime scheduledAt,
        AppointmentStatus status) =>
        new()
        {
            Id = id,
            ScheduledAt = scheduledAt,
            Status = status,
            DisplayName = $"Provider {id}",
            ProviderEmail = $"{id}@example.com"
        };

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

    private static AppointmentPage Page(
        IEnumerable<AppointmentDetail>? items = null,
        long? totalCount = null,
        int pageSize = CalendarViewModel.HistoryPageSize,
        int page = 1) =>
        new(
            items?.ToList() ?? [],
            totalCount ?? items?.LongCount() ?? 0,
            page,
            pageSize);

    private static Mock<ICalendarApiService> AppointmentService(
        List<AppointmentDetail>? scheduled = null,
        AppointmentPage? done = null,
        AppointmentPage? cancelled = null)
    {
        var service = new Mock<ICalendarApiService>();
        service.Setup(api => api.GetAppointmentsPageAsync(
                AppointmentPageSegment.Scheduled, 1, PageRequest.MaxPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(scheduled, pageSize: PageRequest.MaxPageSize));
        service.Setup(api => api.GetAppointmentsPageAsync(
                AppointmentPageSegment.Done, 1, CalendarViewModel.HistoryPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(done ?? Page());
        service.Setup(api => api.GetAppointmentsPageAsync(
                AppointmentPageSegment.Cancelled, 1, CalendarViewModel.HistoryPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cancelled ?? Page());
        return service;
    }

    [Theory]
    [InlineData("Provider")]
    [InlineData("Customer")]
    public async Task LoadAsync_BothRolesSeeTheSameScheduledAppointmentList(string role)
    {
        var now = DateTime.Now;
        var service = AppointmentService(
        [
            Appointment("scheduled", now.AddHours(1), AppointmentStatus.Booked)
        ],
        Page(totalCount: 8),
        Page(totalCount: 3));
        var vm = new CalendarViewModel(service.Object, CreateMockSession(role: role).Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("scheduled", Assert.Single(vm.DisplayedAppointments).Id);
        Assert.Equal(1, vm.ScheduledCount);
        Assert.Equal(8, vm.DoneCount);
        Assert.Equal(3, vm.CancelledCount);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task LoadAsync_NetworkError_SetsHasErrorTrueWithNoFabricatedAppointments()
    {
        var service = AppointmentService();
        service.Setup(s => s.GetAppointmentsPageAsync(
               AppointmentPageSegment.Scheduled, 1, PageRequest.MaxPageSize,
               It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("Network unreachable"));
        var vm = new CalendarViewModel(service.Object, CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.DisplayedAppointments);
        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    [Fact]
    public async Task LoadAsync_EmptyResult_NoFabricatedAppointments()
    {
        var vm = new CalendarViewModel(
            AppointmentService().Object,
            CreateMockSession().Object);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.DisplayedAppointments);
        Assert.True(vm.AppointmentListIsEmpty);
        Assert.False(vm.HasError);
    }

    [Theory]
    [InlineData("Provider")]
    [InlineData("Customer")]
    public async Task HistoryTabsUseServerTotalsAndRetrieveFiveAtATimeForBothRoles(string role)
    {
        var now = DateTime.Now;
        var donePageOne = Enumerable.Range(1, 5)
            .Select(index => Appointment($"done-{index}", now.AddDays(-index), AppointmentStatus.Completed))
            .ToList();
        var donePageTwo = new List<AppointmentDetail>
        {
            Appointment("done-6", now.AddDays(-6), AppointmentStatus.Completed),
            Appointment("done-7", now.AddDays(-7), AppointmentStatus.Completed)
        };
        var service = AppointmentService(
            done: Page(donePageOne, totalCount: 7));
        service.Setup(api => api.GetAppointmentsPageAsync(
                AppointmentPageSegment.Done, 2, CalendarViewModel.HistoryPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(donePageTwo, totalCount: 7, page: 2));
        var vm = new CalendarViewModel(service.Object, CreateMockSession(role: role).Object);

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.SelectAppointmentTabCommand.ExecuteAsync("Done");

        Assert.Equal(5, vm.DisplayedAppointments.Count);
        Assert.Equal(7, vm.DoneCount);
        Assert.True(vm.CanGoForwardInHistory);
        Assert.Equal("1 / 2", vm.HistoryPageLabel);

        await vm.NextHistoryPageCommand.ExecuteAsync(null);

        Assert.Equal(["done-6", "done-7"], vm.DisplayedAppointments.Select(appointment => appointment.Id));
        Assert.True(vm.CanGoBackInHistory);
        Assert.False(vm.CanGoForwardInHistory);
        Assert.Equal("2 / 2", vm.HistoryPageLabel);
    }

    [Fact]
    public async Task NextHistoryPage_WhenRequestFails_StaysOnTheCurrentPageAndShowsError()
    {
        var service = AppointmentService(
            done: Page(
                Enumerable.Range(1, 5)
                    .Select(index => Appointment($"done-{index}", DateTime.Now.AddDays(-index), AppointmentStatus.Completed)),
                totalCount: 7));
        service.Setup(api => api.GetAppointmentsPageAsync(
                AppointmentPageSegment.Done, 2, CalendarViewModel.HistoryPageSize,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        var vm = new CalendarViewModel(service.Object, CreateMockSession().Object);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.SelectAppointmentTabCommand.ExecuteAsync("Done");

        await vm.NextHistoryPageCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.HistoryPage);
        Assert.True(vm.HasError);
        Assert.Equal(5, vm.DisplayedAppointments.Count);
    }

    [Fact]
    public void AppointmentListUsesTheProposedTimeForPendingReschedules()
    {
        var now = DateTime.Now;
        var appointment = new AppointmentDetail
        {
            Id = "reschedule-proposed",
            ScheduledAt = now.AddDays(-2),
            ProposedStart = now.AddDays(1),
            Status = AppointmentStatus.RescheduleRequested
        };

        Assert.Equal(appointment.ProposedStart, appointment.ListTime);
    }

    [Fact]
    public async Task OpenCustomerAppointment_RaisesSelectionForRowsWithAnIdentifier()
    {
        var service = new Mock<ICalendarApiService>();
        var vm = new CalendarViewModel(
            service.Object,
            CreateMockSession(role: "Customer").Object);
        AppointmentDetail? selected = null;
        vm.AppointmentSelected += (_, appointment) => selected = appointment;
        var appointment = Appointment("appointment-1", DateTime.Now.AddDays(1), AppointmentStatus.Booked);

        vm.OpenAppointmentFromListCommand.Execute(appointment);

        Assert.Same(appointment, selected);
    }
}
