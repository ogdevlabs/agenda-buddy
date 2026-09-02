using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// The booking flow is service → date → time, driven entirely by what the provider actually has free.
/// Nothing here types a date or a time, because a slot the provider does not have must not be expressible.
/// </summary>
public class BookAppointmentViewModelTests
{
    private const string Provider = "coach@example.com";
    private const string Customer = "me@example.com";

    private static readonly DateTime Slot9 = new(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Slot10 = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Slot11 = new(2026, 9, 12, 11, 0, 0, DateTimeKind.Utc);

    private static Mock<IUserSessionService> Session(string role = "Customer")
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(role == "Provider" ? Provider : Customer);
        session.SetupGet(s => s.IsProvider).Returns(role == "Provider");
        session.SetupGet(s => s.IsCustomer).Returns(role == "Customer");
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static ServiceItem Svc(string name, string? profession, int? minutes, bool active = true) =>
        new() { Name = name, Description = name, ProfessionName = profession, DurationMinutes = minutes, IsActive = active };

    private static Mock<IServicesApiService> ServicesApi(params ServiceItem[] services)
    {
        var api = new Mock<IServicesApiService>();
        api.Setup(a => a.GetServicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(services.ToList());
        return api;
    }

    private static Mock<ICalendarApiService> CalendarApi(params DateTime[] slots)
    {
        var api = new Mock<ICalendarApiService>();
        api.Setup(a => a.GetProviderAvailabilityAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(CalendarApiService.GroupByDate(slots));
        return api;
    }

    private static BookAppointmentViewModel Build(
        Mock<IServicesApiService> services,
        Mock<ICalendarApiService> calendar,
        Mock<IBookingApiService>? booking = null,
        string role = "Customer")
    {
        booking ??= new Mock<IBookingApiService>();
        return new BookAppointmentViewModel(booking.Object, services.Object, calendar.Object, Session(role).Object)
        {
            CounterpartEmail = Provider,
            CounterpartName = "Pat Coach"
        };
    }

    // Only what the server would accept: inactive or unclassified services are not bookable.
    [Fact]
    public async Task LoadAsync_ShowsOnlyActiveClassifiedServices()
    {
        var vm = Build(
            ServicesApi(
                Svc("Good", "Fitness", 60),
                Svc("Inactive", "Fitness", 60, active: false),
                Svc("Unclassified", null, 60)),
            CalendarApi(Slot9));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Good"], vm.Services.Select(s => s.Name));
    }

    // The directory's profession filter carries through, so a customer browsing "Fitness" is not then
    // offered that provider's unrelated services.
    [Fact]
    public async Task LoadAsync_HonoursTheProfessionScope()
    {
        var vm = Build(ServicesApi(Svc("Lift", "Fitness", 60), Svc("Maths", "Tutoring", 60)), CalendarApi(Slot9));
        vm.ProfessionScope = "Tutoring";

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Maths"], vm.Services.Select(s => s.Name));
    }

    // One service is not a choice — selecting it automatically saves the customer a pointless tap.
    [Fact]
    public async Task LoadAsync_ASingleServiceIsSelectedAutomaticallyAndFetchesAvailability()
    {
        var calendar = CalendarApi(Slot9, Slot10);
        var vm = Build(ServicesApi(Svc("Only", "Fitness", 60)), calendar);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotNull(vm.SelectedService);
        Assert.True(vm.HasBookableDates);
        calendar.Verify(a => a.GetProviderAvailabilityAsync(
            Provider, "Only", BookAppointmentViewModel.WindowDays, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectingAService_GroupsAvailabilityAndLandsOnTheSoonestOpenDate()
    {
        var vm = Build(ServicesApi(Svc("A", "Fitness", 60), Svc("B", "Fitness", 60)), CalendarApi(Slot11, Slot9, Slot10));
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.SelectServiceCommand.ExecuteAsync(vm.Services[0]);

        // Dates are the device's, so they are derived rather than hardcoded.
        Assert.Equal(
            new[] { Slot9, Slot11 }.Select(s => DateOnly.FromDateTime(s.ToLocalTime())).Distinct().OrderBy(d => d),
            vm.BookableDates);
        Assert.Equal(DateOnly.FromDateTime(Slot9.ToLocalTime()), vm.SelectedDate);
        Assert.Equal([Slot9, Slot10], vm.TimesForSelectedDate.Select(t => t.StartUtc));
    }

    // Slot boundaries come from the service's duration, so switching service must not keep a slot that
    // was only valid for the previous one.
    [Fact]
    public async Task ChangingTheServiceClearsTheChosenDateAndSlot_AndRefetches()
    {
        var calendar = CalendarApi(Slot9, Slot10);
        var vm = Build(ServicesApi(Svc("A", "Fitness", 60), Svc("B", "Fitness", 90)), calendar);
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.SelectServiceCommand.ExecuteAsync(vm.Services[0]);
        vm.SelectSlotCommand.Execute(vm.TimesForSelectedDate.Single(t => t.StartUtc == Slot9));
        Assert.NotNull(vm.SelectedSlot);

        await vm.SelectServiceCommand.ExecuteAsync(vm.Services[1]);

        Assert.Null(vm.SelectedSlot);
        calendar.Verify(a => a.GetProviderAvailabilityAsync(
            Provider, "B", BookAppointmentViewModel.WindowDays, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangingTheDateClearsAnAlreadyChosenSlot()
    {
        var vm = Build(ServicesApi(Svc("A", "Fitness", 60)), CalendarApi(Slot9, Slot11));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectSlotCommand.Execute(vm.TimesForSelectedDate.Single(t => t.StartUtc == Slot9));

        vm.SelectDateCommand.Execute(DateOnly.FromDateTime(Slot11.ToLocalTime()));

        Assert.Null(vm.SelectedSlot);
        Assert.Equal([Slot11], vm.TimesForSelectedDate.Select(t => t.StartUtc));
    }

    [Fact]
    public async Task CannotBookUntilBothAServiceAndASlotAreChosen()
    {
        var vm = Build(ServicesApi(Svc("A", "Fitness", 60), Svc("B", "Fitness", 60)), CalendarApi(Slot9));
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.False(vm.CanBook);

        await vm.SelectServiceCommand.ExecuteAsync(vm.Services[0]);
        Assert.False(vm.CanBook);

        vm.SelectSlotCommand.Execute(vm.TimesForSelectedDate.Single(t => t.StartUtc == Slot9));
        Assert.True(vm.CanBook);
    }

    // The slot is the exact instant the server offered, sent back unchanged; the end comes from the
    // service's own duration so the booked length matches what was advertised.
    [Fact]
    public async Task Booking_SendsTheChosenSlotUnchanged_TheServiceName_AndADurationDerivedEnd()
    {
        DateTime? sentStart = null, sentEnd = null;
        string? sentService = null, sentProvider = null, sentCustomer = null;
        var booking = new Mock<IBookingApiService>();
        booking.Setup(b => b.BookAppointmentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTime, DateTime, string?, CancellationToken>(
                (p, c, s, e, svc, _) => { sentProvider = p; sentCustomer = c; sentStart = s; sentEnd = e; sentService = svc; })
            .ReturnsAsync("appt-1");

        var vm = Build(ServicesApi(Svc("Deep Tissue", "Wellness", 90)), CalendarApi(Slot9), booking);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectSlotCommand.Execute(vm.TimesForSelectedDate.Single(t => t.StartUtc == Slot9));

        await vm.BookCommand.ExecuteAsync(null);

        Assert.Equal(Slot9, sentStart);
        Assert.Equal(Slot9.AddMinutes(90), sentEnd);
        Assert.Equal("Deep Tissue", sentService);
        Assert.Equal(Provider, sentProvider);
        Assert.Equal(Customer, sentCustomer);
    }

    // A service with no duration must still be bookable rather than sending a zero-length appointment.
    [Fact]
    public async Task Booking_AServiceWithNoDurationFallsBackToSixtyMinutes()
    {
        DateTime? sentEnd = null;
        var booking = new Mock<IBookingApiService>();
        booking.Setup(b => b.BookAppointmentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, DateTime, DateTime, string?, CancellationToken>(
                (_, _, _, e, _, _) => sentEnd = e)
            .ReturnsAsync("appt-1");

        var vm = Build(ServicesApi(Svc("No Duration", "Wellness", null)), CalendarApi(Slot9), booking);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectSlotCommand.Execute(vm.TimesForSelectedDate.Single(t => t.StartUtc == Slot9));

        await vm.BookCommand.ExecuteAsync(null);

        Assert.Equal(Slot9.AddMinutes(60), sentEnd);
    }

    // Someone else may take the slot between the fetch and the tap; the server rejects the overlap. The
    // stale slot has to disappear rather than be offered again.
    [Fact]
    public async Task Booking_ARejectedSlotRefetchesAvailabilityAndReportsIt()
    {
        var booking = new Mock<IBookingApiService>();
        booking.Setup(b => b.BookAppointmentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var calendar = CalendarApi(Slot9);

        var vm = Build(ServicesApi(Svc("A", "Fitness", 60)), calendar, booking);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectSlotCommand.Execute(vm.TimesForSelectedDate.Single(t => t.StartUtc == Slot9));

        await vm.BookCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("no longer available", vm.ErrorMessage);
        // once on select, once on the refetch
        calendar.Verify(a => a.GetProviderAvailabilityAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Fully booked is a normal state and must not read as an error.
    [Fact]
    public async Task AFullyBookedProviderIsReportedAsSuch_NotAsAnError()
    {
        var vm = Build(ServicesApi(Svc("A", "Fitness", 60)), CalendarApi());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsFullyBooked);
        Assert.False(vm.HasError);
        Assert.False(vm.CanBook);
    }

    [Fact]
    public async Task AProviderWithNoBookableServicesIsReportedAsSuch()
    {
        var vm = Build(ServicesApi(Svc("Unclassified", null, 60)), CalendarApi(Slot9));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasNoServices);
        Assert.False(vm.HasServices);
    }

    // A provider booking a customer offers their OWN catalogue and calendar, not the customer's.
    [Fact]
    public async Task ProviderRole_UsesItsOwnEmailForServicesAndAvailability()
    {
        var services = ServicesApi(Svc("A", "Fitness", 60));
        var calendar = CalendarApi(Slot9);
        var vm = new BookAppointmentViewModel(
            Mock.Of<IBookingApiService>(), services.Object, calendar.Object, Session("Provider").Object)
        {
            CounterpartEmail = Customer,
            CounterpartName = "Me"
        };

        await vm.LoadCommand.ExecuteAsync(null);

        services.Verify(a => a.GetServicesAsync(Provider, It.IsAny<CancellationToken>()), Times.Once);
        calendar.Verify(a => a.GetProviderAvailabilityAsync(
            Provider, "A", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadAsync_ServiceFetchFailure_SurfacesAnError()
    {
        var services = new Mock<IServicesApiService>();
        services.Setup(a => a.GetServicesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("boom"));

        var vm = Build(services, CalendarApi());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsLoading);
    }
}
