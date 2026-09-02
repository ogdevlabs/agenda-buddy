using System.Globalization;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Services;

/// <summary>
/// The 90-day window is fetched once and grouped client-side, so switching dates needs no round trip.
/// </summary>
/// <remarks>
/// Grouping happens on the date the slot falls on FOR THIS DEVICE, while the value keeps the server's UTC
/// instant. These cases derive their expectations from <see cref="TimeZoneInfo.Local"/> rather than
/// hardcoding an offset, so they hold on any machine — including CI in UTC.
/// </remarks>
public class ProviderAvailabilityGroupingTests
{
    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static DateOnly LocalDateOf(DateTime utc) => DateOnly.FromDateTime(utc.ToLocalTime());

    [Fact]
    public void GroupsSlotsByLocalDate_AndOrdersThemWithinEachDate()
    {
        var early = Utc("2026-09-10T09:00:00Z");
        var late = Utc("2026-09-10T15:00:00Z");

        var grouped = CalendarApiService.GroupByDate([late, early]);

        var slots = grouped.SlotsOn(LocalDateOf(early));
        Assert.Equal(early, slots[0].StartUtc);
        Assert.True(slots.SequenceEqual(slots.OrderBy(s => s.StartUtc)));
    }

    // The value must stay the server's instant, because the booking POST sends it back unchanged; the
    // LABEL is the device's reading of it. Conflating them books a different time than was shown.
    [Fact]
    public void KeepsTheUtcInstantButLabelsItInTheDeviceZone()
    {
        var utc = Utc("2026-09-10T17:00:00Z");

        var slot = CalendarApiService.GroupByDate([utc]).SlotsOn(LocalDateOf(utc)).Single();

        Assert.Equal(utc, slot.StartUtc);
        Assert.Equal(DateTimeKind.Utc, slot.StartUtc.Kind);
        Assert.Equal(utc.ToLocalTime(), slot.LocalStart);
        Assert.Equal(utc.ToLocalTime().ToString("h:mm tt"), slot.Label);
    }

    // The defect this grouping fixes: behind UTC, a small-hours UTC slot belongs to the PREVIOUS local
    // evening. Grouping on the UTC date filed it under the wrong day's tile.
    [Fact]
    public void ASlotIsFiledUnderItsLocalDate_NotItsUtcDate()
    {
        var utc = Utc("2026-09-10T01:00:00Z");

        var grouped = CalendarApiService.GroupByDate([utc]);

        Assert.Equal([LocalDateOf(utc)], grouped.BookableDates);
        Assert.Single(grouped.SlotsOn(LocalDateOf(utc)));

        if (TimeZoneInfo.Local.BaseUtcOffset < TimeSpan.Zero)
            Assert.NotEqual(new DateOnly(2026, 9, 10), LocalDateOf(utc));
    }

    // A date with nothing free is ABSENT, not present-and-empty: the calendar tells "bookable" from "full"
    // by presence, so an empty entry would render a selectable day offering no times.
    [Fact]
    public void ADateWithNoSlotsIsAbsentEntirely()
    {
        var utc = Utc("2026-09-09T12:00:00Z");

        var grouped = CalendarApiService.GroupByDate([utc]);
        var untouched = LocalDateOf(utc).AddDays(3);

        Assert.False(grouped.SlotsByDate.ContainsKey(untouched));
        Assert.Empty(grouped.SlotsOn(untouched));
    }

    [Fact]
    public void BookableDatesAreAscending_AndFirstIsTheEarliest()
    {
        var a = Utc("2026-09-20T12:00:00Z");
        var b = Utc("2026-09-11T12:00:00Z");
        var c = Utc("2026-09-15T12:00:00Z");

        var grouped = CalendarApiService.GroupByDate([a, b, c]);

        Assert.Equal(grouped.BookableDates.OrderBy(d => d), grouped.BookableDates);
        Assert.Equal(LocalDateOf(b), grouped.FirstBookableDate);
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

    // A non-UTC-kind value must be normalised before grouping, or it is filed against the wrong day.
    [Fact]
    public void NonUtcInputIsNormalisedBeforeGrouping()
    {
        var utc = Utc("2026-09-10T14:00:00Z");

        var slot = CalendarApiService.GroupByDate([utc.ToLocalTime()])
            .SlotsOn(LocalDateOf(utc)).Single();

        Assert.Equal(utc, slot.StartUtc);
    }
}
