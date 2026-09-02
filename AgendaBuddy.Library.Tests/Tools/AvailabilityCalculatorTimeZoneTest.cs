using System;
using System.Collections.Generic;
using System.Linq;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Library.Tests.Tools;

/// <summary>
/// Business hours are 09:00–19:00 in the PROVIDER'S zone, returned as UTC instants.
/// </summary>
/// <remarks>
/// Generating them in UTC for everyone offered a provider at UTC-6 slots from 03:00 to 13:00 local — the
/// middle of the night. These cases are written against several zones on purpose, so the behaviour is
/// general rather than correct only for whoever happened to run it.
/// </remarks>
public class AvailabilityCalculatorTimeZoneTest
{
    private static ProviderEntity Provider(string? timeZoneId, params AppointmentEntity[] appointments) => new()
    {
        FirstName = "Test",
        LastName = "Provider",
        Email = "coach@example.com",
        TimeZoneId = timeZoneId,
        AppointmentEntities = appointments.ToList()
    };

    private static bool ZoneAvailable(string id)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }

    /// <summary>Local wall-clock hour of a UTC instant, in the given zone.</summary>
    private static int LocalHour(DateTime utc, string zoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(zoneId)).Hour;

    // The headline property, across several offsets — including a half-hour one, which a naive
    // whole-hour implementation gets wrong.
    [Theory]
    [InlineData("America/Mexico_City")]
    [InlineData("Europe/Madrid")]
    [InlineData("Asia/Tokyo")]
    [InlineData("Asia/Kolkata")]
    [InlineData("Pacific/Auckland")]
    public void EverySlotFallsInsideBusinessHoursOfTheProvidersOwnZone(string zoneId)
    {
        if (!ZoneAvailable(zoneId)) return;

        var slots = AvailabilityCalculator.GetAvailability(
            Provider(zoneId), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), days: 10);

        Assert.NotEmpty(slots);
        Assert.All(slots, slot => Assert.InRange(
            LocalHour(slot, zoneId),
            AvailabilityCalculator.OpeningHour,
            AvailabilityCalculator.ClosingHour - 1));
    }

    // The concrete regression: at UTC-6 the old UTC-based generation produced 03:00 local starts.
    [Fact]
    public void AProviderBehindUtcIsNotOfferedMiddleOfTheNightSlots()
    {
        const string zone = "America/Mexico_City";
        if (!ZoneAvailable(zone)) return;

        var slots = AvailabilityCalculator.GetAvailability(
            Provider(zone), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), days: 5);

        Assert.DoesNotContain(slots, slot => LocalHour(slot, zone) < AvailabilityCalculator.OpeningHour);
        Assert.Contains(slots, slot => LocalHour(slot, zone) == AvailabilityCalculator.OpeningHour);
    }

    // A provider ahead of UTC has their working day cross the UTC date boundary; the window must still be
    // their days, not UTC's.
    [Fact]
    public void AProviderAheadOfUtcGetsTheirOwnCalendarDays()
    {
        const string zone = "Pacific/Auckland";
        if (!ZoneAvailable(zone)) return;

        var slots = AvailabilityCalculator.GetAvailability(
            Provider(zone), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), days: 7);

        var localDates = slots
            .Select(slot => TimeZoneInfo.ConvertTimeFromUtc(slot, TimeZoneInfo.FindSystemTimeZoneById(zone)).Date)
            .Distinct()
            .Count();

        Assert.Equal(7, localDates);
    }

    // No zone recorded must behave exactly as before the field existed, so existing providers are unaffected.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoZoneRecordedFallsBackToUtc(string? zoneId)
    {
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var withNothing = AvailabilityCalculator.GetAvailability(Provider(zoneId), now, days: 3);
        var withUtc = AvailabilityCalculator.GetAvailability(Provider("UTC"), now, days: 3);

        Assert.Equal(withUtc, withNothing);
        Assert.All(withNothing, slot => Assert.InRange(
            slot.Hour, AvailabilityCalculator.OpeningHour, AvailabilityCalculator.ClosingHour - 1));
    }

    // A zone this host cannot resolve must not take the whole calendar down — the provider stays bookable
    // on UTC hours instead.
    [Fact]
    public void AnUnknownZoneDegradesToUtcRatherThanThrowing()
    {
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var slots = AvailabilityCalculator.GetAvailability(
            Provider("Mars/Olympus_Mons"), now, days: 2);

        Assert.NotEmpty(slots);
        Assert.Equal(AvailabilityCalculator.GetAvailability(Provider("UTC"), now, days: 2), slots);
    }

    // Spring forward: 02:00–03:00 does not exist locally. Business hours start at 09:00 so no slot is lost
    // here, but the day must still produce a full, duplicate-free grid.
    [Fact]
    public void ASpringForwardDayStillYieldsAFullDistinctGrid()
    {
        const string zone = "America/Mexico_City";
        if (!ZoneAvailable(zone)) return;

        // US/Mexico-style DST change lands in spring; take a wide window so a transition is included.
        var slots = AvailabilityCalculator.GetAvailability(
            Provider(zone), new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc), days: 21);

        Assert.Equal(slots.Count, slots.Distinct().Count());
        Assert.All(slots, slot => Assert.InRange(
            LocalHour(slot, zone), AvailabilityCalculator.OpeningHour, AvailabilityCalculator.ClosingHour - 1));
    }

    // Autumn back: an hour repeats. Each local start must map to exactly ONE instant, or a date shows the
    // same time twice.
    [Fact]
    public void AnAutumnFallBackDayDoesNotProduceDuplicateSlots()
    {
        const string zone = "Europe/Madrid";
        if (!ZoneAvailable(zone)) return;

        var slots = AvailabilityCalculator.GetAvailability(
            Provider(zone), new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), days: 21);

        Assert.Equal(slots.Count, slots.Distinct().Count());

        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var perLocalSlot = slots
            .Select(slot => TimeZoneInfo.ConvertTimeFromUtc(slot, tz))
            .GroupBy(local => local)
            .Where(group => group.Count() > 1)
            .ToList();

        Assert.Empty(perLocalSlot);
    }

    // Duration still cannot run past closing, measured on the provider's clock.
    [Fact]
    public void ALongSessionCannotRunPastClosingInTheProvidersZone()
    {
        const string zone = "Asia/Tokyo";
        if (!ZoneAvailable(zone)) return;

        var slots = AvailabilityCalculator.GetAvailability(
            Provider(zone), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), days: 5, durationMinutes: 120);

        Assert.All(slots, slot => Assert.True(
            LocalHour(slot, zone) <= AvailabilityCalculator.ClosingHour - 2,
            $"{slot:O} starts too late for a 2-hour session"));
    }

    // A booked appointment is a UTC instant; it must still block the right local slot in any zone.
    [Fact]
    public void AnExistingAppointmentBlocksTheCorrectLocalSlot()
    {
        const string zone = "America/Mexico_City";
        if (!ZoneAvailable(zone)) return;

        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        // 10:00 local on 2 June 2026.
        var localStart = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);

        var provider = Provider(zone, new AppointmentEntity
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "customer@example.com",
            Start = startUtc,
            End = startUtc.AddHours(1)
        });

        var slots = AvailabilityCalculator.GetAvailability(
            provider, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), days: 5);

        Assert.DoesNotContain(startUtc, slots);
        Assert.Contains(startUtc.AddHours(1), slots); // back-to-back is still free
    }
}
