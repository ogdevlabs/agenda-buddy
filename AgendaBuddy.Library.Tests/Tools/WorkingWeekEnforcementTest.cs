using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using MongoDB.Bson;
using Xunit;

namespace AgendaBuddy.Library.Tests.Tools;

/// <summary>
/// The working week is a working week by default, and what the calendar offers is what a booking may take.
/// </summary>
/// <remarks>
/// Two defects, both reported from the app:
/// <list type="number">
/// <item>
/// A provider who had configured nothing was offered <b>seven days a week</b>. The legacy single pair describes
/// hours and never which days, so every weekday inherited it — nobody asks for weekend availability by default,
/// and there was no way to discover it was happening.
/// </item>
/// <item>
/// <c>POST /api/v1/booking/appointments</c> validated "in the future", "no overlap" and "a service this provider
/// offers" — and <b>nothing about working hours</b>. A Saturday a provider had explicitly closed was accepted with
/// <c>201 Created</c>. A listing a write does not enforce is a suggestion.
/// </item>
/// </list>
/// </remarks>
public class WorkingWeekEnforcementTest
{
    private const string Zone = "America/Mexico_City";   // UTC-6, no DST since 2022

    private static ProviderEntity Provider(
        List<WorkDayHours>? week = null, int? startHour = null, int? endHour = null) => new()
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Pat",
            LastName = "Coach",
            Email = "coach@example.com",
            TimeZoneId = Zone,
            WorkDayStartHour = startHour,
            WorkDayEndHour = endHour,
            WorkWeek = week ?? [],
        };

    /// <summary>A local wall-clock time in the provider's zone, as a UTC instant.</summary>
    private static DateTime LocalAsUtc(int year, int month, int day, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById(Zone));

    // 2026-09-07 is a Monday, so 12th = Saturday, 13th = Sunday.
    private static readonly DateTime MondayNoonUtc = LocalAsUtc(2026, 9, 7, 12);
    private static readonly DateTime SaturdayNoonUtc = LocalAsUtc(2026, 9, 12, 12);
    private static readonly DateTime SundayNoonUtc = LocalAsUtc(2026, 9, 13, 12);

    // ── the default ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(DayOfWeek.Monday, true)]
    [InlineData(DayOfWeek.Tuesday, true)]
    [InlineData(DayOfWeek.Wednesday, true)]
    [InlineData(DayOfWeek.Thursday, true)]
    [InlineData(DayOfWeek.Friday, true)]
    [InlineData(DayOfWeek.Saturday, false)]
    [InlineData(DayOfWeek.Sunday, false)]
    public void TheDefaultWeekIsMondayToFriday(DayOfWeek day, bool open)
    {
        Assert.Equal(open, AvailabilityCalculator.IsOpenByDefault(day));
    }

    /// <summary>
    /// ⚠️ The regression. A provider who has never opened the calendar screen must not be offered at weekends.
    /// </summary>
    [Fact]
    public void AnUnconfiguredProviderIsOfferedNoWeekendSlots()
    {
        var slots = AvailabilityCalculator.GetAvailability(
            Provider(), nowUtc: LocalAsUtc(2026, 9, 7, 0), days: 14);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(Zone);
        var weekend = slots
            .Select(slot => TimeZoneInfo.ConvertTimeFromUtc(slot, zone).DayOfWeek)
            .Where(day => day is DayOfWeek.Saturday or DayOfWeek.Sunday)
            .ToList();

        Assert.Empty(weekend);
        // The control: the working week is still offered, so the default did not simply close everything.
        Assert.NotEmpty(slots);
    }

    /// <summary>
    /// The legacy single pair sets HOURS, never days — so it must not reopen the weekend.
    /// </summary>
    [Fact]
    public void ALegacyHoursPairDoesNotReopenTheWeekend()
    {
        var slots = AvailabilityCalculator.GetAvailability(
            Provider(startHour: 10, endHour: 16), nowUtc: LocalAsUtc(2026, 9, 7, 0), days: 14);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(Zone);
        Assert.DoesNotContain(
            slots.Select(slot => TimeZoneInfo.ConvertTimeFromUtc(slot, zone).DayOfWeek),
            day => day is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    /// <summary>
    /// ⚠️ <b>The default is only a default.</b> A provider who works Saturdays says so, and is then open.
    /// </summary>
    [Fact]
    public void AnExplicitWeekendEntryOverridesTheDefault()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Saturday, 9, 13)]);

        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(
            provider, SaturdayNoonUtc, SaturdayNoonUtc.AddHours(1)));
    }

    /// <summary>And a weekday explicitly closed is closed, default or not.</summary>
    [Fact]
    public void AnExplicitlyClosedWeekdayIsClosed()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17, isClosed: true)]);

        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(
            provider, MondayNoonUtc, MondayNoonUtc.AddHours(1)));
    }

    // ── enforcement ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ASessionInsideTheWorkingWindowIsAllowed()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17)]);

        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(
            provider, MondayNoonUtc, MondayNoonUtc.AddHours(1)));
    }

    [Fact]
    public void ASessionOnAClosedWeekendDayIsRefused()
    {
        var provider = Provider([
            new WorkDayHours(DayOfWeek.Saturday, 9, 17, isClosed: true),
            new WorkDayHours(DayOfWeek.Sunday, 9, 17, isClosed: true)
        ]);

        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(
            provider, SaturdayNoonUtc, SaturdayNoonUtc.AddHours(1)));
        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(
            provider, SundayNoonUtc, SundayNoonUtc.AddHours(1)));
    }

    [Fact]
    public void ASessionBeforeOpeningIsRefused()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17)]);
        var threeAm = LocalAsUtc(2026, 9, 7, 3);

        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(provider, threeAm, threeAm.AddHours(1)));
    }

    [Fact]
    public void ASessionStartingAtOpeningIsAllowed()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17)]);
        var nine = LocalAsUtc(2026, 9, 7, 9);

        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(provider, nine, nine.AddHours(1)));
    }

    /// <summary>
    /// The end hour is EXCLUSIVE, matching every other use: 17 means the last session finishes at 17:00. So a
    /// session ending exactly at closing fits, and one ending a minute later does not.
    /// </summary>
    [Fact]
    public void ASessionEndingExactlyAtClosingIsAllowedAndOneRunningPastIsNot()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17)]);
        var four = LocalAsUtc(2026, 9, 7, 16);

        Assert.True(AvailabilityCalculator.IsWithinWorkingHours(provider, four, four.AddHours(1)));
        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(provider, four, four.AddHours(1).AddMinutes(1)));
    }

    [Fact]
    public void ASessionStartingAfterClosingIsRefused()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17)]);
        var tenPm = LocalAsUtc(2026, 9, 7, 22);

        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(provider, tenPm, tenPm.AddHours(1)));
    }

    /// <summary>A date the provider has taken off accepts nothing, matching what the calendar offers.</summary>
    [Fact]
    public void ASessionOnADayOffIsRefused()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17)]);
        provider.AppointmentEntities =
        [
            new AppointmentEntity
            {
                Id = ObjectId.GenerateNewId(),
                EmailProvider = provider.Email,
                EmailCustomer = "nobody@example.com",
                Start = LocalAsUtc(2026, 9, 7, 0),
                End = LocalAsUtc(2026, 9, 8, 0),
                DayOff = true,
            }
        ];

        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(
            provider, MondayNoonUtc, MondayNoonUtc.AddHours(1)));
    }

    [Fact]
    public void AZeroLengthSessionDescribesNoIntervalAndIsRefused()
    {
        var provider = Provider([new WorkDayHours(DayOfWeek.Monday, 9, 17)]);

        Assert.False(AvailabilityCalculator.IsWithinWorkingHours(provider, MondayNoonUtc, MondayNoonUtc));
    }

    /// <summary>
    /// ⚠️ <b>The invariant that matters most: what is offered is exactly what is accepted.</b> Enforcement and
    /// generation share <c>ResolveHours</c>, so this holds by construction — and this test is what keeps a future
    /// change from restating the rule in one place only.
    /// </summary>
    [Fact]
    public void EveryOfferedSlotIsAcceptedByTheEnforcementCheck()
    {
        var provider = Provider([
            new WorkDayHours(DayOfWeek.Monday, 9, 17),
            new WorkDayHours(DayOfWeek.Tuesday, 9, 13),
            new WorkDayHours(DayOfWeek.Wednesday, 9, 17, isClosed: true),
            new WorkDayHours(DayOfWeek.Saturday, 10, 14),
        ]);

        var slots = AvailabilityCalculator.GetAvailability(
            provider, nowUtc: LocalAsUtc(2026, 9, 7, 0), days: 21);

        Assert.NotEmpty(slots);
        Assert.All(slots, slot => Assert.True(
            AvailabilityCalculator.IsWithinWorkingHours(provider, slot, slot.AddHours(1)),
            $"{slot:O} was offered but would be refused at booking."));
    }
}
