using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// A provider sets their own working week — seven rows, one per weekday.
/// </summary>
/// <remarks>
/// <para>
/// The end hour is exclusive, so the pickers and the saved payload have to agree on what "ends at 17:00" means,
/// and a day that opens at or after it closes has to be refused rather than quietly corrected.
/// </para>
/// <para>
/// The load-bearing assertion in here is the FALLBACK: every provider stored before per-weekday hours existed has
/// an empty week, and the rows have to be seeded from their single pair. Otherwise this screen opens as seven
/// blanks and saving turns it into a change they never intended.
/// </para>
/// </remarks>
public class CalendarSettingsViewModelTests
{
    private const string Email = "coach@example.com";

    private static Mock<IUserSessionService> Session(bool isProvider = true)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(Email);
        session.SetupGet(s => s.Role).Returns(isProvider ? "provider" : "customer");
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.SetupGet(s => s.IsCustomer).Returns(!isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static CalendarSettingsViewModel Build(
        Mock<IProviderApiService> providerApi, bool isProvider = true) =>
        new(providerApi.Object, Session(isProvider).Object);

    private static Mock<IProviderApiService> Api(
        WorkHours? stored, List<WorkDayHoursDto>? week = null, AppointmentActionResult? saveResult = null)
    {
        var api = new Mock<IProviderApiService>();
        api.Setup(p => p.GetWorkHoursAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(stored);
        api.Setup(p => p.GetWorkWeekAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(week ?? []);
        api.Setup(p => p.UpdateWorkWeekAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<WorkDayHoursDto>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(saveResult ?? AppointmentActionResult.Done());
        return api;
    }

    private static WorkDayRow Day(CalendarSettingsViewModel vm, DayOfWeek day) =>
        vm.Days.Single(row => row.Day == day);

    [Fact]
    public void SevenRowsAreOfferedMondayFirst()
    {
        var vm = Build(Api(WorkHours.Default));

        Assert.Equal(7, vm.Days.Count);
        Assert.Equal(DayOfWeek.Monday, vm.Days[0].Day);
        Assert.Equal(DayOfWeek.Sunday, vm.Days[^1].Day);
    }

    /// <summary>
    /// ⚠️ <b>An unconfigured week is a WORKING week — Monday to Friday open, the weekend closed.</b>
    /// </summary>
    /// <remarks>
    /// It used to open all seven, because the legacy single pair describes hours and never which days, so every
    /// weekday inherited it. A provider who had never opened this screen was therefore offered to customers on
    /// Saturday and Sunday, with nothing telling them so. The rule comes from
    /// <c>AvailabilityCalculator.IsOpenByDefault</c>, which is also what the server generates availability from —
    /// so this screen cannot show a week the calendar does not honour.
    /// </remarks>
    [Fact]
    public void BeforeAnythingLoadsTheWeekIsMondayToFridayWithTheWeekendClosed()
    {
        var vm = Build(Api(WorkHours.Default));

        Assert.All(vm.Days, day =>
        {
            // The hours are seeded on every row, closed or not, so re-opening a day does not mean re-entering
            // them — the same reason IsClosed is kept separate from the hours in the first place.
            Assert.Equal(8, day.StartHour);
            Assert.Equal(17, day.EndHour);
            Assert.Equal(day.Day is DayOfWeek.Saturday or DayOfWeek.Sunday, day.IsClosed);
        });
    }

    [Fact]
    public void ThePickersCoverEveryHourAStartAndAnEndCanTake()
    {
        var vm = Build(Api(WorkHours.Default));

        Assert.Equal(24, vm.StartHourOptions.Count);
        Assert.Equal("00:00", vm.StartHourOptions[0]);
        Assert.Equal("23:00", vm.StartHourOptions[^1]);

        // The end runs to 24:00, which means midnight — a day CAN end there, but cannot start there.
        Assert.Equal(24, vm.EndHourOptions.Count);
        Assert.Equal("01:00", vm.EndHourOptions[0]);
        Assert.Equal("24:00", vm.EndHourOptions[^1]);
    }

    /// <summary>
    /// The fallback. A provider who has only ever set the single pair must see THAT on every row, not blanks.
    /// </summary>
    [Fact]
    public async Task AnEmptyStoredWeekIsSeededFromTheSinglePair()
    {
        var vm = Build(Api(new WorkHours(10, 15), week: []));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.All(vm.Days, day =>
        {
            Assert.Equal(10, day.StartHour);
            Assert.Equal(15, day.EndHour);
        });
    }

    [Fact]
    public async Task AStoredWeekdayWinsOverTheFallbackForThatDayOnly()
    {
        var vm = Build(Api(
            new WorkHours(9, 17),
            week: [new WorkDayHoursDto(DayOfWeek.Saturday, 7, 11, false)]));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(7, Day(vm, DayOfWeek.Saturday).StartHour);
        Assert.Equal(11, Day(vm, DayOfWeek.Saturday).EndHour);

        Assert.Equal(9, Day(vm, DayOfWeek.Monday).StartHour);
        Assert.Equal(17, Day(vm, DayOfWeek.Monday).EndHour);
    }

    [Fact]
    public async Task AStoredClosedDayLoadsAsClosed()
    {
        var vm = Build(Api(
            WorkHours.Default,
            week: [new WorkDayHoursDto(DayOfWeek.Sunday, null, null, true)]));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(Day(vm, DayOfWeek.Sunday).IsClosed);
        Assert.False(Day(vm, DayOfWeek.Monday).IsClosed);
    }

    /// <summary>
    /// An unusable stored pair is treated as unconfigured rather than as closed, matching the server: a provider
    /// silently unbookable is worse than one on their fallback hours.
    /// </summary>
    [Fact]
    public async Task AnUnusableStoredDayFallsBackRatherThanClosingIt()
    {
        var vm = Build(Api(
            new WorkHours(9, 17),
            week: [new WorkDayHoursDto(DayOfWeek.Tuesday, 17, 9, false)]));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(Day(vm, DayOfWeek.Tuesday).IsClosed);
        Assert.Equal(9, Day(vm, DayOfWeek.Tuesday).StartHour);
        Assert.Equal(17, Day(vm, DayOfWeek.Tuesday).EndHour);
    }

    [Fact]
    public async Task AProviderWhoCannotBeReadSeesAnErrorRatherThanInventedHours()
    {
        var vm = Build(Api(stored: null));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task AThrowingReadSurfacesAnErrorRatherThanPropagating()
    {
        var api = new Mock<IProviderApiService>();
        api.Setup(p => p.GetWorkHoursAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new HttpRequestException("down"));
        var vm = Build(api);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
    }

    /// <summary>
    /// Every one of the seven rows is sent, so a weekday left on its fallback is stored explicitly rather than
    /// left to inherit — otherwise saving a week would silently leave gaps behind.
    /// </summary>
    [Fact]
    public async Task SavingSendsAllSevenDaysAsTheRowsShowThem()
    {
        List<WorkDayHoursDto>? sent = null;
        var api = Api(WorkHours.Default);
        api.Setup(p => p.UpdateWorkWeekAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<WorkDayHoursDto>>(), It.IsAny<CancellationToken>()))
           .Callback<string, IEnumerable<WorkDayHoursDto>, CancellationToken>((_, days, _) => sent = days.ToList())
           .ReturnsAsync(AppointmentActionResult.Done());

        var vm = Build(api);
        await vm.LoadCommand.ExecuteAsync(null);

        Day(vm, DayOfWeek.Monday).StartHourIndex = 6;    // 06:00
        Day(vm, DayOfWeek.Monday).EndHourIndex = 11;     // 12:00 — the options start at 1
        Day(vm, DayOfWeek.Sunday).IsClosed = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(sent);
        Assert.Equal(7, sent!.Count);

        var monday = sent.Single(day => day.Day == DayOfWeek.Monday);
        Assert.Equal(6, monday.StartHour);
        Assert.Equal(12, monday.EndHour);
        Assert.False(monday.IsClosed);

        Assert.True(sent.Single(day => day.Day == DayOfWeek.Sunday).IsClosed);
    }

    [Fact]
    public async Task SavingRaisesSavedSoThePageCanReturnToTheCalendar()
    {
        var vm = Build(Api(WorkHours.Default));
        var raised = false;
        vm.Saved += (_, _) => raised = true;

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(raised);
    }

    /// <summary>
    /// The server detail stays diagnostic while the banner uses localized validation copy.
    /// </summary>
    [Fact]
    public async Task ARejectedSaveShowsLocalizedValidationCopy()
    {
        var vm = Build(Api(
            WorkHours.Default,
            saveResult: new AppointmentActionResult(
                false, new MobileError(
                    MobileOperation.WorkWeek,
                    MobileErrorCategory.Validation,
                    "These days do not describe a usable window: Wednesday."))));

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Check the information you entered and try again.", vm.ErrorMessage);
    }

    [Fact]
    public async Task AnUnreachableServerKeepsThePageOpenToo()
    {
        var vm = Build(Api(WorkHours.Default, saveResult: AppointmentActionResult.Unreachable()));

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
    }

    [Theory]
    [InlineData(17, 8)]   // 17:00 to 09:00 — the end index is one behind the hour
    [InlineData(9, 8)]    // 09:00 to 09:00
    public async Task ADayThatDoesNotOpenBeforeItClosesBlocksTheWholeSave(int startIndex, int endIndex)
    {
        var vm = Build(Api(WorkHours.Default));
        await vm.LoadCommand.ExecuteAsync(null);

        Day(vm, DayOfWeek.Wednesday).StartHourIndex = startIndex;
        Day(vm, DayOfWeek.Wednesday).EndHourIndex = endIndex;

        Assert.False(Day(vm, DayOfWeek.Wednesday).IsValid);
        Assert.False(vm.IsWeekValid);
        Assert.False(vm.SaveCommand.CanExecute(null));

        // Named per day, so the provider is told which one is wrong rather than that something is.
        Assert.Contains("Wednesday", Day(vm, DayOfWeek.Wednesday).ErrorMessage);
    }

    /// <summary>A closed day has no window, so it cannot be the invalid one.</summary>
    [Fact]
    public async Task ClosingAnInvalidDayMakesTheWeekSaveable()
    {
        var vm = Build(Api(WorkHours.Default));
        await vm.LoadCommand.ExecuteAsync(null);

        Day(vm, DayOfWeek.Wednesday).StartHourIndex = 17;
        Day(vm, DayOfWeek.Wednesday).EndHourIndex = 8;
        Assert.False(vm.IsWeekValid);

        Day(vm, DayOfWeek.Wednesday).IsClosed = true;

        Assert.True(vm.IsWeekValid);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ADayEndingAtMidnightIsValid()
    {
        var vm = Build(Api(WorkHours.Default));
        await vm.LoadCommand.ExecuteAsync(null);

        Day(vm, DayOfWeek.Friday).StartHourIndex = 20;
        Day(vm, DayOfWeek.Friday).EndHourIndex = 23;    // 24:00

        Assert.Equal(24, Day(vm, DayOfWeek.Friday).EndHour);
        Assert.True(Day(vm, DayOfWeek.Friday).IsValid);
    }

    /// <summary>
    /// Closing a day keeps its hours, so re-opening it does not mean re-entering them.
    /// </summary>
    [Fact]
    public async Task ClosingAndReopeningADayKeepsItsHours()
    {
        var vm = Build(Api(new WorkHours(11, 14)));
        await vm.LoadCommand.ExecuteAsync(null);

        var monday = Day(vm, DayOfWeek.Monday);
        monday.IsClosed = true;
        monday.IsClosed = false;

        Assert.Equal(11, monday.StartHour);
        Assert.Equal(14, monday.EndHour);
    }

    /// <summary>
    /// Without this, setting up a normal week is fourteen pickers.
    /// </summary>
    [Fact]
    public async Task CopyingTheFirstOpenDaySpreadsItsHoursToEveryOtherOpenDay()
    {
        var vm = Build(Api(WorkHours.Default));
        await vm.LoadCommand.ExecuteAsync(null);

        Day(vm, DayOfWeek.Sunday).IsClosed = true;
        Day(vm, DayOfWeek.Monday).StartHourIndex = 6;
        Day(vm, DayOfWeek.Monday).EndHourIndex = 11;    // 12:00

        await vm.CopyFirstDayToAllCommand.ExecuteAsync(null);

        foreach (var day in vm.Days.Where(day => !day.IsClosed))
        {
            Assert.Equal(6, day.StartHour);
            Assert.Equal(12, day.EndHour);
        }

        // A closed day is left closed: "copy hours" is not an instruction to start working that day.
        Assert.True(Day(vm, DayOfWeek.Sunday).IsClosed);
    }

    /// <summary>
    /// Warned about, not blocked — a provider may genuinely be shutting up shop for a while.
    /// </summary>
    [Fact]
    public async Task AWeekWithEveryDayClosedIsSaveableButFlagged()
    {
        var vm = Build(Api(WorkHours.Default));
        await vm.LoadCommand.ExecuteAsync(null);

        foreach (var day in vm.Days) day.IsClosed = true;

        Assert.True(vm.IsFullyClosed);
        Assert.True(vm.IsWeekValid);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// The summary names the CLOSED days: a provider scanning it is checking they have not shut one by accident,
    /// which is the mistake that costs bookings silently.
    /// </summary>
    [Fact]
    public async Task TheSummaryNamesTheClosedDays()
    {
        var vm = Build(Api(WorkHours.Default));
        await vm.LoadCommand.ExecuteAsync(null);

        Day(vm, DayOfWeek.Sunday).IsClosed = true;

        Assert.Contains("Sun", vm.WeekSummary);
    }

    [Fact]
    public async Task OnlyAProviderHasACalendarToConfigure()
    {
        var vm = Build(Api(WorkHours.Default), isProvider: false);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsProvider);
    }
}
