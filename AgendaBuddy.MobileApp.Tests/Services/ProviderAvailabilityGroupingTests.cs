using System.Globalization;
using AgendaBuddy.MobileApp.Services;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

/// <summary>
/// The 90-day window is fetched once and grouped client-side, so switching dates in the calendar needs no
/// round trip. Grouping is what lets the calendar show which dates are bookable at all.
/// </summary>
public class ProviderAvailabilityGroupingTests
{
    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    [Fact]
    public void GroupsSlotsByDate_AndOrdersThemWithinEachDate()
    {
        var grouped = CalendarApiService.GroupByDate(
        [
            Utc("2026-09-10T15:00:00Z"),
            Utc("2026-09-09T09:00:00Z"),
            Utc("2026-09-10T09:00:00Z"),
        ]);

        Assert.Equal(2, grouped.SlotsByDate.Count);
        Assert.Equal(
            [Utc("2026-09-10T09:00:00Z"), Utc("2026-09-10T15:00:00Z")],
            grouped.SlotsOn(new DateOnly(2026, 9, 10)));
    }

    // A date with nothing free is ABSENT, not present-and-empty. The calendar tells "bookable" from "full"
    // by presence, so an empty entry would render a selectable day that then offers no times.
    [Fact]
    public void ADateWithNoSlotsIsAbsentEntirely()
    {
        var grouped = CalendarApiService.GroupByDate([Utc("2026-09-09T09:00:00Z")]);

        Assert.False(grouped.SlotsByDate.ContainsKey(new DateOnly(2026, 9, 10)));
        Assert.Empty(grouped.SlotsOn(new DateOnly(2026, 9, 10)));
    }

    [Fact]
    public void BookableDatesAreAscending_AndFirstIsTheEarliest()
    {
        var grouped = CalendarApiService.GroupByDate(
        [
            Utc("2026-09-20T09:00:00Z"),
            Utc("2026-09-11T09:00:00Z"),
            Utc("2026-09-15T09:00:00Z"),
        ]);

        Assert.Equal(
            [new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 20)],
            grouped.BookableDates);
        Assert.Equal(new DateOnly(2026, 9, 11), grouped.FirstBookableDate);
    }

    // A fully-booked provider is a normal state, not an error, and must not look like one.
    [Fact]
    public void AFullyBookedProviderGroupsToNothingRatherThanThrowing()
    {
        var grouped = CalendarApiService.GroupByDate([]);

        Assert.False(grouped.HasAny);
        Assert.Null(grouped.FirstBookableDate);
        Assert.Empty(grouped.BookableDates);
    }

    // Slots must stay the exact instants the server sent, because the booking POST sends one back
    // unchanged. A local-kind value grouped under a local date would file it under the wrong day.
    [Fact]
    public void NonUtcInputIsNormalisedBeforeGrouping()
    {
        var utc = Utc("2026-09-10T02:00:00Z");

        var grouped = CalendarApiService.GroupByDate([utc.ToLocalTime()]);

        Assert.Equal([utc], grouped.SlotsOn(new DateOnly(2026, 9, 10)));
    }
}
