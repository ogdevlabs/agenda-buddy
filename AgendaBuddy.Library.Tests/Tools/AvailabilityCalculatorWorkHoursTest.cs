using System;
using System.Collections.Generic;
using System.Linq;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Library.Tests.Tools;

/// <summary>
/// The working window is the provider's own, and falls back to a standard day when they have not set one.
/// "Now" is always supplied, so nothing here depends on the machine's clock; no TimeZoneId is set, so the
/// provider's clock is UTC and every expectation is expressible in plain UTC.
/// </summary>
public class AvailabilityCalculatorWorkHoursTest
{
    // A Monday, just after midnight UTC, so the whole of "today" is still ahead.
    private static readonly DateTime NowUtc = new(2026, 9, 7, 0, 5, 0, DateTimeKind.Utc);

    private static ProviderEntity Provider(int? startHour = null, int? endHour = null) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = "coach@example.com",
        AppointmentEntities = [],
        WorkDayStartHour = startHour,
        WorkDayEndHour = endHour
    };

    private static List<int> HoursOnFirstDay(ProviderEntity provider) =>
        AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 1)
            .Select(slot => slot.Hour)
            .ToList();

    [Fact]
    public void AProviderWhoHasNotSetHoursGetsAStandardEightToFiveDay()
    {
        var hours = HoursOnFirstDay(Provider());

        Assert.Equal([8, 9, 10, 11, 12, 13, 14, 15, 16], hours);
    }

    [Fact]
    public void TheDefaultsAreEightAndSeventeen()
    {
        Assert.Equal(8, AvailabilityCalculator.DefaultOpeningHour);
        Assert.Equal(17, AvailabilityCalculator.DefaultClosingHour);
    }

    [Fact]
    public void TheProvidersOwnHoursAreUsedWhenTheyHaveSetThem()
    {
        var hours = HoursOnFirstDay(Provider(startHour: 6, endHour: 10));

        Assert.Equal([6, 7, 8, 9], hours);
    }

    [Fact]
    public void TheEndHourIsExclusiveSoTheLastSessionFinishesOnIt()
    {
        // 10:00-11:00 is the last hour that fits before an 11:00 close.
        var hours = HoursOnFirstDay(Provider(startHour: 9, endHour: 11));

        Assert.Equal([9, 10], hours);
    }

    [Fact]
    public void AnEveningWindowIsHonouredRatherThanCappedAtTheOldNineToSeven()
    {
        var hours = HoursOnFirstDay(Provider(startHour: 18, endHour: 22));

        Assert.Equal([18, 19, 20, 21], hours);
    }

    [Fact]
    public void AWindowEndingAtMidnightIncludesTheTwentyThirdHour()
    {
        var hours = HoursOnFirstDay(Provider(startHour: 22, endHour: 24));

        Assert.Equal([22, 23], hours);
    }

    [Fact]
    public void AOneHourWindowYieldsExactlyOneSlot()
    {
        var hours = HoursOnFirstDay(Provider(startHour: 12, endHour: 13));

        Assert.Equal([12], hours);
    }

    [Theory]
    // A start at or past the end leaves no bookable time at all.
    [InlineData(17, 8)]
    [InlineData(9, 9)]
    // Out of range in either direction.
    [InlineData(-1, 17)]
    [InlineData(24, 25)]
    [InlineData(8, 0)]
    [InlineData(8, 25)]
    // One half set without the other cannot describe a window.
    [InlineData(9, null)]
    [InlineData(null, 17)]
    public void AnUnusableStoredWindowFallsBackToTheDefaultRatherThanEmptyingTheCalendar(int? start, int? end)
    {
        var hours = HoursOnFirstDay(Provider(start, end));

        Assert.Equal([8, 9, 10, 11, 12, 13, 14, 15, 16], hours);
    }

    [Fact]
    public void ALongSessionStillHasToFinishByTheProvidersOwnClosingHour()
    {
        var slots = AvailabilityCalculator.GetAvailability(
            Provider(startHour: 9, endHour: 12), NowUtc, days: 1, durationMinutes: 120);

        // 09:00 and 10:00 fit two hours before noon; 11:00 does not.
        Assert.Equal([9, 10], slots.Select(slot => slot.Hour).ToList());
    }

    [Fact]
    public void CustomHoursApplyToEveryDayInTheWindowNotJustTheFirst()
    {
        var slots = AvailabilityCalculator.GetAvailability(Provider(startHour: 7, endHour: 9), NowUtc, days: 3);

        Assert.Equal(6, slots.Count);
        Assert.All(slots, slot => Assert.InRange(slot.Hour, 7, 8));
    }
}
