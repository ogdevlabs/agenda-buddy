using System;
using System.Collections.Generic;
using System.Linq;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Library.Tests.Tools;

/// <summary>
/// Per-weekday working hours. Hours are resolved for each date in the window, not once for the whole of it.
/// </summary>
/// <remarks>
/// No <c>TimeZoneId</c> is set, so the provider's clock is UTC and every expectation is plain UTC. "Now" is
/// always supplied, so nothing here reads the machine clock.
/// </remarks>
public class AvailabilityCalculatorWorkWeekTest
{
    // A Monday, just after midnight UTC, so the whole of "today" is still ahead.
    private static readonly DateTime MondayUtc = new(2026, 9, 7, 0, 5, 0, DateTimeKind.Utc);

    private static ProviderEntity Provider(
        int? legacyStart = null,
        int? legacyEnd = null,
        params WorkDayHours[] workWeek) => new()
        {
            FirstName = "Test",
            LastName = "Provider",
            Email = "coach@example.com",
            AppointmentEntities = [],
            WorkDayStartHour = legacyStart,
            WorkDayEndHour = legacyEnd,
            WorkWeek = [.. workWeek]
        };

    private static List<int> HoursOn(ProviderEntity provider, int dayOffset) =>
        AvailabilityCalculator.GetAvailability(provider, MondayUtc, days: dayOffset + 1)
            .Where(slot => slot.Date == MondayUtc.Date.AddDays(dayOffset))
            .Select(slot => slot.Hour)
            .ToList();

    [Fact]
    public void AWeekdayWithItsOwnHoursUsesThem()
    {
        var provider = Provider(workWeek: new WorkDayHours(DayOfWeek.Monday, 10, 13));

        Assert.Equal([10, 11, 12], HoursOn(provider, 0));
    }

    [Fact]
    public void EachWeekdayGetsItsOwnAnswerInsideOneWindow()
    {
        var provider = Provider(
            workWeek:
            [
                new WorkDayHours(DayOfWeek.Monday, 9, 11),
                new WorkDayHours(DayOfWeek.Tuesday, 14, 16)
            ]);

        Assert.Equal([9, 10], HoursOn(provider, 0));
        Assert.Equal([14, 15], HoursOn(provider, 1));
    }

    [Fact]
    public void AWeekdayMarkedClosedYieldsNothingAtAll()
    {
        var provider = Provider(workWeek: new WorkDayHours(DayOfWeek.Monday, null, null, isClosed: true));

        Assert.Empty(HoursOn(provider, 0));
    }

    /// <summary>
    /// Closing a day must not empty the rest of the week — the commonest real configuration is exactly this.
    /// </summary>
    [Fact]
    public void ClosingOneDayLeavesTheOthersBookable()
    {
        var provider = Provider(workWeek: new WorkDayHours(DayOfWeek.Monday, null, null, isClosed: true));

        Assert.Empty(HoursOn(provider, 0));
        Assert.NotEmpty(HoursOn(provider, 1));
    }

    /// <summary>
    /// Closed days keep whatever hours were stored with them, so a provider re-opening a day does not have to
    /// re-enter them. The flag wins while it is set.
    /// </summary>
    [Fact]
    public void ClosedWinsOverHoursStoredAlongsideIt()
    {
        var provider = Provider(workWeek: new WorkDayHours(DayOfWeek.Monday, 9, 17, isClosed: true));

        Assert.Empty(HoursOn(provider, 0));
    }

    /// <summary>
    /// This is what keeps every provider stored before per-weekday hours existed bookable on exactly the hours
    /// they had. An empty work week must behave identically to the single pair.
    /// </summary>
    [Fact]
    public void AWeekdayWithNoEntryInheritsTheSingleLegacyPair()
    {
        var provider = Provider(legacyStart: 11, legacyEnd: 14);

        Assert.Equal([11, 12, 13], HoursOn(provider, 0));
    }

    [Fact]
    public void AWeekdayWithNoEntryAndNoLegacyPairGetsTheStandardDay()
    {
        var opening = AvailabilityCalculator.DefaultOpeningHour;
        var closing = AvailabilityCalculator.DefaultClosingHour;

        // Closing is exclusive: the last hour-long session starts one hour before it.
        Assert.Equal(
            [.. Enumerable.Range(opening, closing - opening)],
            HoursOn(Provider(), 0));
    }

    /// <summary>
    /// A specific weekday's entry overrides the legacy pair for that day only, so partially configuring a week
    /// is a coherent state rather than a broken one.
    /// </summary>
    [Fact]
    public void APerDayEntryOverridesTheLegacyPairForThatDayOnly()
    {
        var provider = Provider(9, 17, new WorkDayHours(DayOfWeek.Tuesday, 6, 8));

        Assert.Equal([9, 10, 11, 12, 13, 14, 15, 16], HoursOn(provider, 0));
        Assert.Equal([6, 7], HoursOn(provider, 1));
    }

    /// <summary>
    /// An unusable pair is treated as unconfigured, not as closed: "0 to 0" is far likelier to be a bad write
    /// than a deliberate closure, and a provider silently unbookable is worse than one on standard hours. Saying
    /// closed takes the flag.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(17, 9)]
    [InlineData(null, 17)]
    [InlineData(9, null)]
    public void AnUnusableStoredPairFallsBackRatherThanClosingTheDay(int? start, int? end)
    {
        var provider = Provider(11, 14, new WorkDayHours(DayOfWeek.Monday, start, end));

        Assert.Equal([11, 12, 13], HoursOn(provider, 0));
    }

    /// <summary>
    /// A document written twice must not produce two conflicting windows for one weekday.
    /// </summary>
    [Fact]
    public void ADuplicateWeekdayEntryResolvesToTheFirst()
    {
        var provider = Provider(
            workWeek:
            [
                new WorkDayHours(DayOfWeek.Monday, 9, 11),
                new WorkDayHours(DayOfWeek.Monday, 15, 18)
            ]);

        Assert.Equal([9, 10], HoursOn(provider, 0));
    }

    /// <summary>
    /// A session must finish by that day's own closing hour, so a long service is not offered a start it cannot
    /// complete in.
    /// </summary>
    [Fact]
    public void ALongSessionRespectsThatDaysClosingHour()
    {
        var provider = Provider(workWeek: new WorkDayHours(DayOfWeek.Monday, 9, 12));

        var slots = AvailabilityCalculator.GetAvailability(provider, MondayUtc, days: 1, durationMinutes: 120)
            .Select(slot => slot.Hour)
            .ToList();

        Assert.Equal([9, 10], slots);
    }

    [Fact]
    public void WorkDayHours_DescribesAWindow_MatchesTheHoursTheCalculatorAccepts()
    {
        Assert.True(new WorkDayHours(DayOfWeek.Monday, 9, 17).DescribesAWindow);
        Assert.False(new WorkDayHours(DayOfWeek.Monday, 9, 17, isClosed: true).DescribesAWindow);
        Assert.False(new WorkDayHours(DayOfWeek.Monday, 17, 9).DescribesAWindow);
        Assert.False(new WorkDayHours(DayOfWeek.Monday, 9, 9).DescribesAWindow);
        Assert.False(new WorkDayHours(DayOfWeek.Monday, null, 17).DescribesAWindow);
        Assert.False(new WorkDayHours(DayOfWeek.Monday, 9, null).DescribesAWindow);
    }
}
