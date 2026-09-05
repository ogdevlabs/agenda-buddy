using System;
using System.Collections.Generic;
using System.Linq;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Library.Tests.Tools;

/// <summary>
/// Pins the properties the previous 30-day helper got wrong. Every case supplies "now" explicitly, so
/// none of these depend on the machine's clock or timezone — the latent flake
/// <c>docs/pdlc/context/11-testing.md</c> documents for <c>AvailabilityScheduleTest</c>.
/// </summary>
public class AvailabilityCalculatorTest
{
    // A Monday, mid-morning, so "today" always has remaining slots.
    private static readonly DateTime NowUtc = new(2026, 9, 7, 10, 30, 0, DateTimeKind.Utc);

    // No TimeZoneId, so UTC applies — the behaviour a provider had before the field existed, which keeps
    // every expectation below expressible in plain UTC. Zone-specific behaviour is covered separately in
    // AvailabilityCalculatorTimeZoneTest.
    private static ProviderEntity Provider(params AppointmentEntity[] appointments) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = "coach@example.com",
        AppointmentEntities = appointments.ToList()
    };

    private static AppointmentEntity Appt(
        DateTime startUtc,
        int minutes,
        bool dayOff = false,
        AppointmentStatus status = AppointmentStatus.Booked) => new()
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "customer@example.com",
            Start = startUtc,
            End = startUtc.AddMinutes(minutes),
            DayOff = dayOff,
            AppointmentStatus = status
        };

    private static DateTime At(int dayOffset, int hour) =>
        new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc).AddDays(dayOffset).AddHours(hour);

    [Fact]
    public void EverySlotIsInTheFutureAndInsideBusinessHours()
    {
        var slots = AvailabilityCalculator.GetAvailability(Provider(), NowUtc, days: 5);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.True(s > NowUtc, $"{s:O} is not in the future"));
        Assert.All(slots, s => Assert.Equal(DateTimeKind.Utc, s.Kind));
        Assert.All(slots, s => Assert.InRange(s.Hour, AvailabilityCalculator.DefaultOpeningHour, AvailabilityCalculator.DefaultClosingHour - 1));
    }

    // The defect that mattered most: a long appointment used to block only the hour it STARTED in, so
    // the hours it actually covered were still offered as free.
    [Fact]
    public void ASlotOverlappedByALongerAppointmentIsExcluded_NotJustItsStartHour()
    {
        var provider = Provider(Appt(At(1, 13), minutes: 180)); // 13:00–16:00

        var slots = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 2);
        var day = slots.Where(s => s.Date == At(1, 0).Date).ToList();

        Assert.DoesNotContain(At(1, 13), day);
        Assert.DoesNotContain(At(1, 14), day); // the hours the old version still offered
        Assert.DoesNotContain(At(1, 15), day);
        Assert.Contains(At(1, 12), day);
        Assert.Contains(At(1, 16), day);       // ends exactly as the appointment ends
    }

    /// <summary>
    /// A CANCELLED appointment frees its slot.
    /// </summary>
    /// <remarks>
    /// Load-bearing since cancellation became a soft delete: the row stays in the provider's embedded list, so
    /// without the status filter a cancelled session would keep its slot blocked forever and every cancellation
    /// would permanently shrink the provider's bookable calendar.
    /// </remarks>
    [Fact]
    public void ACancelledAppointmentDoesNotBlockItsSlot()
    {
        var provider = Provider(Appt(At(1, 13), minutes: 180, status: AppointmentStatus.Cancelled));

        var day = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 2)
            .Where(s => s.Date == At(1, 0).Date)
            .ToList();

        Assert.Contains(At(1, 13), day);
        Assert.Contains(At(1, 14), day);
        Assert.Contains(At(1, 15), day);
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.Completed)]
    public void EveryNonCancelledAppointmentStillBlocksItsSlot(AppointmentStatus status)
    {
        // A pending request holds the slot too -- offering it to somebody else while the provider decides is
        // how two people end up booked into one hour.
        var provider = Provider(Appt(At(1, 13), minutes: 60, status: status));

        var day = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 2)
            .Where(s => s.Date == At(1, 0).Date)
            .ToList();

        Assert.DoesNotContain(At(1, 13), day);
    }

    // A day off is a day off regardless of status -- a cancelled DAY OFF is not a thing anyone sets, and
    // treating one as bookable would reopen a day the provider closed.
    [Fact]
    public void ADayOffStillBlocksItsWholeDateEvenIfMarkedCancelled()
    {
        var provider = Provider(
            Appt(At(2, 9), minutes: 60, dayOff: true, status: AppointmentStatus.Cancelled));

        var slots = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 4);

        Assert.DoesNotContain(At(2, 0).Date, slots.Select(s => s.Date));
    }

    // A requested duration must not be allowed to run INTO an existing appointment either.
    [Fact]
    public void ASlotWhoseDurationWouldRunIntoAnAppointmentIsExcluded()
    {
        var provider = Provider(Appt(At(1, 15), minutes: 60)); // 15:00–16:00

        var ninety = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 2, durationMinutes: 90);

        Assert.DoesNotContain(At(1, 14), ninety); // 14:00–15:30 would collide
        Assert.Contains(At(1, 13), ninety);       // 13:00–14:30 fits
    }

    [Fact]
    public void ADayOffBlocksTheWholeDate()
    {
        var provider = Provider(Appt(At(2, 9), minutes: 60, dayOff: true));

        var slots = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 4);

        Assert.DoesNotContain(slots, s => s.Date == At(2, 0).Date);
        Assert.Contains(slots, s => s.Date == At(3, 0).Date);
    }

    [Fact]
    public void ALongerDurationCannotRunPastClosing()
    {
        var slots = AvailabilityCalculator.GetAvailability(Provider(), NowUtc, days: 2, durationMinutes: 120);

        var latest = slots.Where(s => s.Date == At(1, 0).Date).Max();
        Assert.Equal(At(1, AvailabilityCalculator.DefaultClosingHour - 2), latest);
    }

    [Fact]
    public void BackToBackBookingIsAllowed_IntervalsAreHalfOpen()
    {
        var provider = Provider(Appt(At(1, 11), minutes: 60)); // 11:00–12:00

        var slots = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 2, durationMinutes: 60);

        Assert.Contains(At(1, 12), slots); // starts exactly when the other ends
        Assert.Contains(At(1, 10), slots); // ends exactly when the other starts
        Assert.DoesNotContain(At(1, 11), slots);
    }

    [Theory]
    [InlineData(0, 1)]                                     // below the floor
    [InlineData(-5, 1)]
    [InlineData(500, AvailabilityCalculator.MaxDays)]       // above the ceiling
    public void TheWindowIsClamped(int requested, int expectedDays)
    {
        var slots = AvailabilityCalculator.GetAvailability(Provider(), NowUtc, days: requested);

        var distinctDates = slots.Select(s => s.Date).Distinct().Count();
        Assert.InRange(distinctDates, 1, expectedDays);
        Assert.All(slots, s => Assert.True(s < NowUtc.Date.AddDays(expectedDays + 1)));
    }

    [Fact]
    public void NinetyDaysIsAcceptedInFull()
    {
        var slots = AvailabilityCalculator.GetAvailability(Provider(), NowUtc, days: 90);

        Assert.Equal(90, slots.Select(s => s.Date).Distinct().Count());
    }

    // A service saved without a duration must still be bookable, not silently yield an empty calendar.
    [Fact]
    public void AMissingDurationFallsBackRatherThanReturningNothing()
    {
        var slots = AvailabilityCalculator.GetAvailability(Provider(), NowUtc, days: 2, durationMinutes: 0);

        Assert.NotEmpty(slots);
    }

    // Older rows predate End being meaningful; a zero-length interval would let a slot slide underneath.
    [Fact]
    public void AnAppointmentWithNoUsableEndStillBlocksItsDefaultLength()
    {
        var provider = Provider(new AppointmentEntity
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "customer@example.com",
            Start = At(1, 14),
            End = At(1, 14) // degenerate
        });

        var slots = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 2);

        Assert.DoesNotContain(At(1, 14), slots);
    }

    // Appointments are persisted UTC; a local-kind row must not shift the busy interval.
    [Fact]
    public void AppointmentTimesAreNormalisedToUtcBeforeComparing()
    {
        var utc = At(1, 13);
        var provider = Provider(new AppointmentEntity
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "customer@example.com",
            Start = utc.ToLocalTime(),
            End = utc.ToLocalTime().AddHours(1)
        });

        var slots = AvailabilityCalculator.GetAvailability(provider, NowUtc, days: 2);

        Assert.DoesNotContain(utc, slots);
    }
}
