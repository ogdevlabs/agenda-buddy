using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// A provider's time off: a time RANGE, which is the whole reason this exists.
/// </summary>
/// <remarks>
/// The mechanism this replaces was a whole-day flag, so an afternoon off cost the entire day. The two assertions
/// worth reading here are the exclusive whole-day end (a block "for the 8th" has to end at the 9th's midnight, or
/// the 8th's last hours stay bookable) and the conflict count being nullable — "could not ask" and "nothing in the
/// way" must not arrive worded the same.
/// </remarks>
public class TimeOffViewModelTests
{
    private const string Email = "coach@example.com";

    private static readonly DateTime Monday = new(2026, 9, 14);

    private static Mock<IUserSessionService> Session(bool isProvider = true)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(Email);
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.SetupGet(s => s.IsCustomer).Returns(!isProvider);
        session.Setup(s => s.RefreshAsync()).Returns(Task.CompletedTask);
        return session;
    }

    private static Mock<ICalendarBlockApiService> Api(
        List<CalendarBlock>? blocks = null,
        int? conflicts = 0,
        AppointmentActionResult? saveResult = null,
        AppointmentActionResult? removeResult = null)
    {
        var api = new Mock<ICalendarBlockApiService>();
        api.Setup(a => a.GetBlocksAsync(It.IsAny<CancellationToken>())).ReturnsAsync(blocks ?? []);
        api.Setup(a => a.CountConflictsAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(conflicts);
        api.Setup(a => a.BlockAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
           .ReturnsAsync(saveResult ?? AppointmentActionResult.Done());
        api.Setup(a => a.RemoveBlockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(removeResult ?? AppointmentActionResult.Done());
        return api;
    }

    private static TimeOffViewModel Build(
        Mock<ICalendarBlockApiService> api, bool isProvider = true) =>
        new(api.Object, Session(isProvider).Object);

    // ── The range ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A whole-day block ends at the NEXT day's midnight. The end is exclusive, so ending it at the same
    /// midnight it starts would block nothing at all.
    /// </summary>
    [Fact]
    public void AWholeDayBlockRunsToTheFollowingMidnight()
    {
        var vm = Build(Api());
        vm.IsAllDay = true;
        vm.StartDate = Monday;
        vm.EndDate = Monday;

        var (start, end) = vm.Range;

        Assert.Equal(Monday, start);
        Assert.Equal(Monday.AddDays(1), end);
        Assert.True(vm.IsRangeValid);
    }

    [Fact]
    public void AMultiDayWholeDayBlockCoversTheLastDayToo()
    {
        var vm = Build(Api());
        vm.IsAllDay = true;
        vm.StartDate = Monday;
        vm.EndDate = Monday.AddDays(2);

        var (start, end) = vm.Range;

        Assert.Equal(Monday, start);

        // Three days off means ending at the fourth day's midnight, or the third stays bookable.
        Assert.Equal(Monday.AddDays(3), end);
        Assert.Contains("3 days", vm.RangeSummary);
    }

    [Fact]
    public void APartDayBlockUsesTheTimesEntered()
    {
        var vm = Build(Api());
        vm.IsAllDay = false;
        vm.StartDate = Monday;
        vm.EndDate = Monday;
        vm.StartTime = new TimeSpan(13, 0, 0);
        vm.EndTime = new TimeSpan(17, 0, 0);

        var (start, end) = vm.Range;

        Assert.Equal(Monday.AddHours(13), start);
        Assert.Equal(Monday.AddHours(17), end);
        Assert.True(vm.IsRangeValid);
    }

    /// <summary>
    /// Refused rather than clamped: widening or flipping a provider's time off blocks hours they never chose.
    /// </summary>
    [Fact]
    public void ARangeThatDoesNotEndAfterItStartsCannotBeSaved()
    {
        var vm = Build(Api());
        vm.IsAllDay = false;
        vm.StartDate = Monday;
        vm.EndDate = Monday;
        vm.StartTime = new TimeSpan(17, 0, 0);
        vm.EndTime = new TimeSpan(13, 0, 0);

        Assert.False(vm.IsRangeValid);
        Assert.False(vm.CanSave);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Contains("end after it starts", vm.RangeSummary);
    }

    [Fact]
    public void TimePickersOnlyApplyToAPartDayBlock()
    {
        var vm = Build(Api());

        vm.IsAllDay = true;
        Assert.False(vm.ShowTimePickers);

        vm.IsAllDay = false;
        Assert.True(vm.ShowTimePickers);
    }

    // ── Conflicts ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shown before saving, so the provider is not met by a refusal they could have seen coming.
    /// </summary>
    [Fact]
    public async Task ConflictsAreReportedWithTheirCount()
    {
        var vm = Build(Api(conflicts: 2));
        vm.StartDate = Monday;
        vm.EndDate = Monday;

        await vm.CheckConflictsCommand.ExecuteAsync(null);

        Assert.True(vm.HasConflicts);
        Assert.Contains("2 booked sessions", vm.ConflictMessage);

        // Blocking does not cancel anything, and saying so is the point: otherwise the provider assumes it did.
        Assert.Contains("will not cancel", vm.ConflictMessage);
    }

    [Fact]
    public async Task NoConflictsShowsNothingRatherThanAZero()
    {
        var vm = Build(Api(conflicts: 0));
        vm.StartDate = Monday;
        vm.EndDate = Monday;

        await vm.CheckConflictsCommand.ExecuteAsync(null);

        Assert.False(vm.HasConflicts);
        Assert.Empty(vm.ConflictMessage);
    }

    /// <summary>
    /// <c>null</c> means the question could not be answered, and must NOT read as "nothing in the way" — that
    /// would tell the provider their range is clear on a dropped connection.
    /// </summary>
    [Fact]
    public async Task AnUnanswerableConflictCheckDoesNotClaimTheRangeIsClear()
    {
        var vm = Build(Api(conflicts: null));
        vm.StartDate = Monday;
        vm.EndDate = Monday;

        await vm.CheckConflictsCommand.ExecuteAsync(null);

        Assert.Null(vm.ConflictCount);
        Assert.False(vm.HasConflicts);
        Assert.Empty(vm.ConflictMessage);
    }

    /// <summary>
    /// A previous answer was about a different range, so editing the pickers has to discard it.
    /// </summary>
    [Fact]
    public async Task EditingTheRangeDiscardsThePreviousConflictAnswer()
    {
        var vm = Build(Api(conflicts: 3));
        vm.StartDate = Monday;
        vm.EndDate = Monday;
        await vm.CheckConflictsCommand.ExecuteAsync(null);
        Assert.True(vm.HasConflicts);

        vm.EndDate = Monday.AddDays(4);

        Assert.Null(vm.ConflictCount);
        Assert.False(vm.HasConflicts);
    }

    [Fact]
    public async Task AnInvalidRangeIsNotCheckedAtAll()
    {
        var api = Api(conflicts: 5);
        var vm = Build(api);
        vm.IsAllDay = false;
        vm.StartDate = Monday;
        vm.EndDate = Monday;
        vm.StartTime = new TimeSpan(17, 0, 0);
        vm.EndTime = new TimeSpan(9, 0, 0);

        await vm.CheckConflictsCommand.ExecuteAsync(null);

        Assert.Null(vm.ConflictCount);
        api.Verify(a => a.CountConflictsAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Saving and removing ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Once the provider has SEEN the conflict count, the save forces past the server's refusal — a second
    /// refusal would be telling them something they have already accepted.
    /// </summary>
    [Fact]
    public async Task SavingAfterSeeingConflictsForcesPastTheRefusal()
    {
        var api = Api(conflicts: 1);
        var vm = Build(api);
        vm.StartDate = Monday;
        vm.EndDate = Monday;

        await vm.CheckConflictsCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        api.Verify(a => a.BlockAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SavingWithNoKnownConflictsDoesNotForce()
    {
        var api = Api(conflicts: 0);
        var vm = Build(api);
        vm.StartDate = Monday;
        vm.EndDate = Monday;

        await vm.SaveCommand.ExecuteAsync(null);

        api.Verify(a => a.BlockAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABlankReasonIsSentAsNullRatherThanEmpty()
    {
        string? sent = "unset";
        var api = Api();
        api.Setup(a => a.BlockAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
           .Callback<DateTime, DateTime, string?, bool, CancellationToken>((_, _, reason, _, _) => sent = reason)
           .ReturnsAsync(AppointmentActionResult.Done());

        var vm = Build(api);
        vm.StartDate = Monday;
        vm.EndDate = Monday;
        vm.Reason = "   ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(sent);
    }

    [Fact]
    public async Task ARejectedSaveShowsAnActionableConflictMessage()
    {
        var vm = Build(Api(saveResult: new AppointmentActionResult(false, new MobileError(
            MobileOperation.CalendarBlock,
            MobileErrorCategory.Conflict,
            "3 booked sessions fall inside this range."))));
        vm.StartDate = Monday;
        vm.EndDate = Monday;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Booked sessions fall inside this time. Review them before blocking it.", vm.ErrorMessage);
    }

    [Fact]
    public async Task ASuccessfulSaveClearsTheReasonAndReloads()
    {
        var api = Api();
        var vm = Build(api);
        vm.StartDate = Monday;
        vm.EndDate = Monday;
        vm.Reason = "Holiday";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(vm.Reason);
        api.Verify(a => a.GetBlocksAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A cancelled trip that still blocks the calendar costs bookings the provider never meant to refuse.
    /// </summary>
    [Fact]
    public async Task RemovingABlockReloadsTheList()
    {
        var api = Api(blocks: [new CalendarBlock("abc", Monday, Monday.AddDays(1), "Holiday")]);
        var vm = Build(api);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RemoveCommand.ExecuteAsync(vm.Blocks[0]);

        api.Verify(a => a.RemoveBlockAsync("abc", It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(a => a.GetBlocksAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ARejectedRemovalSaysSoRatherThanLookingLikeItWorked()
    {
        var vm = Build(Api(
            blocks: [new CalendarBlock("abc", Monday, Monday.AddDays(1), null)],
            removeResult: new AppointmentActionResult(false, new MobileError(
                MobileOperation.CalendarBlock,
                MobileErrorCategory.NotFound,
                "No such block for this provider."))));

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.RemoveCommand.ExecuteAsync(vm.Blocks[0]);

        Assert.True(vm.HasError);
    }

    // ── Reading ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A failed read must NOT show the empty state: telling the provider they have no time off when the request
    /// failed says their calendar is open when it may not be.
    /// </summary>
    [Fact]
    public async Task AFailedLoadShowsAnErrorAndNotTheEmptyState()
    {
        var api = new Mock<ICalendarBlockApiService>();
        api.Setup(a => a.GetBlocksAsync(It.IsAny<CancellationToken>()))
           .ThrowsAsync(new HttpRequestException("down"));
        var vm = Build(api);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.ShowEmptyState);
    }

    [Fact]
    public void TheEmptyStateIsNotClaimedBeforeAnythingLoads()
    {
        var vm = Build(Api());

        Assert.False(vm.ShowEmptyState);
        Assert.False(vm.HasLoaded);
    }

    [Fact]
    public async Task AnEmptyListAfterASuccessfulLoadShowsTheEmptyState()
    {
        var vm = Build(Api(blocks: []));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.ShowEmptyState);
        Assert.False(vm.HasBlocks);
    }

    /// <summary>
    /// A whole-day block must not read as "8 Sep 00:00 – 9 Sep 00:00", which is nobody's description of a day
    /// off.
    /// </summary>
    [Fact]
    public void AWholeDayBlockReadsAsADayOff()
    {
        var block = new CalendarBlock("a", Monday, Monday.AddDays(1), null);

        Assert.True(block.IsAllDay);
        Assert.Contains("all day", block.RangeLabel);
        Assert.DoesNotContain("00:00", block.RangeLabel);
    }

    [Fact]
    public void APartDayBlockNamesItsHours()
    {
        var block = new CalendarBlock("a", Monday.AddHours(13), Monday.AddHours(17), "Dentist");

        Assert.False(block.IsAllDay);
        Assert.Contains(Monday.AddHours(13).ToString("t", System.Globalization.CultureInfo.CurrentCulture), block.RangeLabel);
        Assert.Contains(Monday.AddHours(17).ToString("t", System.Globalization.CultureInfo.CurrentCulture), block.RangeLabel);
        Assert.True(block.HasReason);
    }

    [Fact]
    public void ARefreshFlagIsSeparateFromTheGeneralLoadingFlag()
    {
        var vm = Build(Api());

        // Sharing them starts a refresh on every OnAppearing, which on iOS leaves the control's inset behind as
        // a blank band above the list that only a manual pull clears.
        Assert.False(vm.IsRefreshing);
        Assert.False(vm.IsLoading);
    }
}
