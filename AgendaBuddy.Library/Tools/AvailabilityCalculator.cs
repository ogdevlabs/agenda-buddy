namespace AgendaBuddy.Library.Tools;

/// <summary>
/// Computes a provider's free booking slots.
/// </summary>
/// <remarks>
/// <para>
/// Replaces <c>SupportTools&lt;T&gt;.GetThirtyDaysCalendarAvailability</c> for anything customer-facing.
/// That method has three defects this one exists to avoid, all of them previously documented in
/// <c>docs/pdlc/context/04-data-access.md</c> and <c>11-testing.md</c>:
/// </para>
/// <list type="number">
/// <item>
/// <b>It only excluded booked START times.</b> A two-hour appointment blocked one hour, so the hour it
/// actually overlapped was still offered. Harmless while no service had a duration; a double-booking
/// generator once services carry one. This compares whole intervals instead.
/// </item>
/// <item>
/// <b>It mixed local and UTC.</b> <c>DateTime.Today</c>/<c>DateTime.Now</c> are local, appointment
/// starts are persisted UTC, so exclusions were wrong by the machine's offset and its own tests passed
/// or failed depending on the timezone CI ran in. Everything here is UTC, and "now" is a parameter, so
/// the result is deterministic.
/// </item>
/// <item>
/// <b>It ignored <c>day_off</c>.</b> A day marked off still offered its full slot grid.
/// </item>
/// </list>
/// <para>
/// ⚠️ <b>Business hours are still fixed at 09:00–19:00 and interpreted as UTC</b>, because there is
/// nowhere to store a provider's hours or timezone — <c>ProviderEntity</c> has no such field (the
/// long-standing F-005 gap, <c>05-data-model.md</c>). That is a real limitation, not an oversight here:
/// this class makes the calculation consistent and testable, it does not invent schedule storage.
/// </para>
/// </remarks>
public static class AvailabilityCalculator
{
    /// <summary>First bookable hour, UTC. See the class remarks on why this is not per-provider.</summary>
    public const int OpeningHourUtc = 9;

    /// <summary>The hour by which a session must have ENDED, UTC.</summary>
    public const int ClosingHourUtc = 19;

    /// <summary>Spacing between candidate start times.</summary>
    public const int SlotStepMinutes = 60;

    /// <summary>Assumed session length when the chosen service does not declare one.</summary>
    public const int DefaultDurationMinutes = 60;

    /// <summary>Largest window a caller may request, so one request cannot ask for years of slots.</summary>
    public const int MaxDays = 90;

    /// <summary>
    /// Free start times for <paramref name="provider"/> in <c>[nowUtc, nowUtc + days)</c>, in UTC, that
    /// can accommodate a session of <paramref name="durationMinutes"/> without overlapping an existing
    /// appointment or running past closing.
    /// </summary>
    /// <param name="provider">Whose calendar to compute. Its embedded appointments are the busy set.</param>
    /// <param name="nowUtc">The instant to treat as "now". Slots at or before it are already gone.</param>
    /// <param name="days">Window length in days, clamped to <c>[1, <see cref="MaxDays"/>]</c>.</param>
    /// <param name="durationMinutes">
    /// Session length. Non-positive values fall back to <see cref="DefaultDurationMinutes"/>, so a
    /// service with no duration set still yields slots rather than none.
    /// </param>
    public static List<DateTime> GetAvailability(
        ProviderEntity provider,
        DateTime nowUtc,
        int days = 30,
        int durationMinutes = DefaultDurationMinutes)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var window = Math.Clamp(days, 1, MaxDays);
        var duration = TimeSpan.FromMinutes(durationMinutes > 0 ? durationMinutes : DefaultDurationMinutes);

        // A day-off entry blocks its whole date, so those dates never produce candidates at all. Every
        // other appointment contributes a busy interval. An appointment with no sensible End (older rows
        // predate it being required) is treated as one default-length session rather than as zero-length,
        // which would let a slot slide underneath it.
        var appointments = provider.AppointmentEntities ?? [];
        var daysOff = appointments
            .Where(appointment => appointment.DayOff)
            .Select(appointment => appointment.Start.ToUniversalTime().Date)
            .ToHashSet();

        var busy = appointments
            .Where(appointment => !appointment.DayOff)
            .Select(appointment =>
            {
                var start = appointment.Start.ToUniversalTime();
                var end = appointment.End.ToUniversalTime();
                if (end <= start) end = start.Add(TimeSpan.FromMinutes(DefaultDurationMinutes));
                return (Start: start, End: end);
            })
            .ToList();

        var slots = new List<DateTime>();
        var firstDate = nowUtc.Date;

        for (var offset = 0; offset < window; offset++)
        {
            var date = firstDate.AddDays(offset);
            if (daysOff.Contains(date)) continue;

            var open = DateTime.SpecifyKind(date.AddHours(OpeningHourUtc), DateTimeKind.Utc);
            var close = DateTime.SpecifyKind(date.AddHours(ClosingHourUtc), DateTimeKind.Utc);

            for (var slot = open; slot + duration <= close; slot = slot.AddMinutes(SlotStepMinutes))
            {
                if (slot <= nowUtc) continue;

                // Half-open intervals: an appointment ending exactly when a slot starts is not a clash,
                // so back-to-back sessions remain bookable.
                var clashes = busy.Any(b => slot < b.End && b.Start < slot + duration);
                if (!clashes) slots.Add(slot);
            }
        }

        return slots;
    }
}
