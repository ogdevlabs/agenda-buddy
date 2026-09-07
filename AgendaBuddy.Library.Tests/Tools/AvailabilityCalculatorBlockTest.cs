using System;
using System.Collections.Generic;
using System.Linq;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Library.Tests.Tools;

/// <summary>
/// Time off is subtracted as an interval, not as a whole date.
/// </summary>
/// <remarks>
/// This is the defect the replaced mechanism had by construction: a block was an appointment with
/// <c>day_off = true</c> and no time range, so blocking an afternoon cost the whole day. No <c>TimeZoneId</c> is
/// set, so the provider's clock is UTC and every expectation is plain UTC.
/// </remarks>
public class AvailabilityCalculatorBlockTest
{
    // A Monday, just after midnight UTC, so the whole of "today" is still ahead. Default hours: 08:00-17:00.
    private static readonly DateTime MondayUtc = new(2026, 9, 7, 0, 5, 0, DateTimeKind.Utc);

    private static ProviderEntity Provider() => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = "coach@example.com",
        AppointmentEntities = []
    };

    private static CalendarBlockEntity Block(DateTime start, DateTime end, string? reason = null) =>
        new("coach@example.com", start, end, reason);

    private static DateTime OnMonday(int hour) => MondayUtc.Date.AddHours(hour);

    private static List<int> Hours(params CalendarBlockEntity[] blocks) =>
        AvailabilityCalculator.GetAvailability(Provider(), MondayUtc, days: 1, blocks: blocks)
            .Select(slot => slot.Hour)
            .ToList();

    [Fact]
    public void NoBlocks_LeavesTheWholeDayBookable()
    {
        Assert.Equal([8, 9, 10, 11, 12, 13, 14, 15, 16], Hours());
    }

    /// <summary>
    /// The whole reason blocks carry a time range: an afternoon off must leave the morning bookable.
    /// </summary>
    [Fact]
    public void BlockingAnAfternoonLeavesTheMorningBookable()
    {
        Assert.Equal([8, 9, 10, 11], Hours(Block(OnMonday(12), OnMonday(17))));
    }

    [Fact]
    public void BlockingTheWholeDayLeavesNothing()
    {
        Assert.Empty(Hours(Block(OnMonday(0), OnMonday(24))));
    }

    /// <summary>
    /// Half-open, matching appointments: a block ending exactly when a slot starts is not a clash, so a provider
    /// who blocks until noon is bookable at noon.
    /// </summary>
    [Fact]
    public void ABlockEndingWhenASlotStartsIsNotAClash()
    {
        Assert.Contains(12, Hours(Block(OnMonday(9), OnMonday(12))));
    }

    [Fact]
    public void ABlockStartingWhenASlotEndsIsNotAClash()
    {
        // 08:00-09:00 is the slot; a block from 09:00 leaves it intact.
        Assert.Contains(8, Hours(Block(OnMonday(9), OnMonday(17))));
    }

    /// <summary>
    /// A slot that merely OVERLAPS a block is gone, not only one that starts inside it — the same whole-interval
    /// comparison appointments get, and the reason a 2-hour session cannot slide underneath a 1-hour block.
    /// </summary>
    [Fact]
    public void ALongSessionOverlappingABlockIsExcluded()
    {
        var slots = AvailabilityCalculator.GetAvailability(
                Provider(), MondayUtc, days: 1, durationMinutes: 120,
                blocks: [Block(OnMonday(11), OnMonday(12))])
            .Select(slot => slot.Hour)
            .ToList();

        // 10:00 would run 10:00-12:00 and straddle the block, so it must not be offered; 08:00 and 09:00 finish
        // before it opens... 09:00 runs to 11:00, which touches but does not overlap.
        Assert.DoesNotContain(10, slots);
        Assert.Contains(9, slots);
        Assert.Contains(12, slots);
    }

    /// <summary>
    /// Multi-day is one long interval, not a per-day expansion. The replaced implementation looped over whole
    /// days and wrote nothing at all when start and end fell on the same date.
    /// </summary>
    [Fact]
    public void AMultiDayBlockCoversEveryDayItSpans()
    {
        var slots = AvailabilityCalculator.GetAvailability(
            Provider(), MondayUtc, days: 5,
            blocks: [Block(MondayUtc.Date, MondayUtc.Date.AddDays(3))]);

        Assert.DoesNotContain(slots, slot => slot < MondayUtc.Date.AddDays(3));
        Assert.Contains(slots, slot => slot >= MondayUtc.Date.AddDays(3));
    }

    /// <summary>
    /// A block within a single day is exactly the case the old day-based loop dropped.
    /// </summary>
    [Fact]
    public void ASameDayBlockIsNotSilentlyIgnored()
    {
        Assert.DoesNotContain(10, Hours(Block(OnMonday(10), OnMonday(11))));
    }

    [Fact]
    public void SeveralBlocksOnOneDayAllApply()
    {
        var hours = Hours(
            Block(OnMonday(9), OnMonday(10)),
            Block(OnMonday(14), OnMonday(15)));

        Assert.DoesNotContain(9, hours);
        Assert.DoesNotContain(14, hours);
        Assert.Contains(8, hours);
        Assert.Contains(13, hours);
    }

    /// <summary>
    /// A malformed row must block nothing rather than emptying the calendar. The interval is rejected at the API
    /// boundary, so reaching here means the row predates that check or was written around it.
    /// </summary>
    [Theory]
    [InlineData(12, 12)]
    [InlineData(17, 9)]
    public void ABlockThatDoesNotOpenBeforeItClosesBlocksNothing(int startHour, int endHour)
    {
        Assert.Equal(
            [8, 9, 10, 11, 12, 13, 14, 15, 16],
            Hours(Block(OnMonday(startHour), OnMonday(endHour))));
    }

    [Fact]
    public void ABlockEntirelyInThePastDoesNotAffectTheWindow()
    {
        Assert.Equal(
            [8, 9, 10, 11, 12, 13, 14, 15, 16],
            Hours(Block(MondayUtc.AddDays(-10), MondayUtc.AddDays(-9))));
    }

    /// <summary>
    /// A block that started before the window and runs into it is still in force. Filtering on start rather than
    /// end would drop exactly the multi-day blocks this mechanism exists for.
    /// </summary>
    [Fact]
    public void ABlockStartedBeforeTheWindowStillApplies()
    {
        Assert.Empty(Hours(Block(MondayUtc.AddDays(-3), OnMonday(24))));
    }

    /// <summary>
    /// The older whole-day <c>day_off</c> appointment flag is still honoured, so nothing already blocked becomes
    /// bookable by this change. Nothing new writes one.
    /// </summary>
    [Fact]
    public void TheLegacyDayOffFlagStillBlocksItsWholeDate()
    {
        var provider = Provider();
        provider.AppointmentEntities.Add(new AppointmentEntity
        {
            EmailProvider = provider.Email,
            EmailCustomer = string.Empty,
            Start = OnMonday(10),
            End = OnMonday(11),
            DayOff = true
        });

        Assert.Empty(AvailabilityCalculator.GetAvailability(provider, MondayUtc, days: 1));
    }

    [Fact]
    public void Overlaps_IsHalfOpenOnBothSides()
    {
        var block = Block(OnMonday(10), OnMonday(12));

        Assert.True(block.Overlaps(OnMonday(11), OnMonday(13)));
        Assert.True(block.Overlaps(OnMonday(9), OnMonday(11)));
        Assert.True(block.Overlaps(OnMonday(9), OnMonday(13)));
        Assert.False(block.Overlaps(OnMonday(12), OnMonday(13)));
        Assert.False(block.Overlaps(OnMonday(9), OnMonday(10)));
    }

    [Fact]
    public void Overlaps_IsFalseForAnIntervalThatDoesNotOpenBeforeItCloses()
    {
        Assert.False(Block(OnMonday(12), OnMonday(12)).Overlaps(OnMonday(0), OnMonday(24)));
    }
}
