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
/// <b>Business hours are the provider's own</b> — <see cref="ProviderEntity.WorkDayStartHour"/> to
/// <see cref="ProviderEntity.WorkDayEndHour"/>, on their own clock
/// (<see cref="ProviderEntity.TimeZoneId"/>) — converted to UTC instants on the way out. Generating them
/// in UTC for everyone — which is what this did until 2026-08-29 — offered a provider at UTC-6 slots from
/// 03:00 to 13:00 local, i.e. in the middle of the night. A provider with no zone recorded is treated as
/// UTC, which is precisely the behaviour they had before the field existed.
/// </para>
/// <para>
/// A provider who has not set their hours gets <see cref="DefaultOpeningHour"/>–<see cref="DefaultClosingHour"/>.
/// A stored pair that does not describe a usable window (out of range, or start at or after end) is ignored
/// in favour of that default rather than yielding an empty calendar.
/// </para>
/// <para>
/// <b>Hours are per weekday</b> — <see cref="ProviderEntity.WorkWeek"/> — resolved for each date in the window
/// rather than once for the whole of it, so a provider who works Saturday mornings and not Sundays is offered
/// correctly. A weekday with no entry inherits the single legacy pair, and a weekday marked closed yields
/// nothing at all. See <see cref="ResolveHours(ProviderEntity, DayOfWeek)"/> for the three tiers.
/// </para>
/// <para>
/// <b>Time off is subtracted as an interval</b> — <see cref="CalendarBlockEntity"/> — not as a whole date, so
/// blocking an afternoon leaves the morning bookable. Blocks are half-open like everything else here. The older
/// whole-day <c>day_off</c> appointment flag is still honoured for rows already written, so nothing already
/// blocked becomes bookable; nothing new writes one.
/// </para>
/// <para>
/// DST is handled rather than ignored: a local start that does not exist (the spring-forward gap) is
/// skipped, and an ambiguous one (the autumn repeat) resolves to a single instant, so a transition day
/// never yields a duplicate or an impossible slot.
/// </para>
/// </remarks>
public static class AvailabilityCalculator
{
    /// <summary>First bookable hour for a provider who has not set their own, in their own timezone.</summary>
    public const int DefaultOpeningHour = 8;

    /// <summary>
    /// The hour by which a session must have ENDED, for a provider who has not set their own. Exclusive:
    /// 17 means the last session finishes at 17:00.
    /// </summary>
    public const int DefaultClosingHour = 17;

    /// <summary>Used when a provider has no timezone recorded — their behaviour before the field existed.</summary>
    public const string FallbackTimeZoneId = "UTC";

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
    /// <param name="blocks">
    /// The provider's time-off blocks. Subtracted as INTERVALS, not whole dates, so an afternoon off leaves the
    /// morning bookable. Optional so existing callers that have no block store are unaffected; a caller that
    /// omits them offers slots inside the provider's time off, which is why the query handler must pass them.
    /// </param>
    public static List<DateTime> GetAvailability(
        ProviderEntity provider,
        DateTime nowUtc,
        int days = 30,
        int durationMinutes = DefaultDurationMinutes,
        IEnumerable<CalendarBlockEntity>? blocks = null)
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

        // A CANCELLED appointment is not busy time. Cancellation is a soft delete, so the row stays in the
        // provider's embedded list — without this clause a cancelled session would keep its slot blocked
        // forever, which is the opposite of what cancelling means and would silently shrink a provider's
        // bookable calendar every time anyone called one off.
        var busy = appointments
            .Where(appointment => !appointment.DayOff
                                  && appointment.AppointmentStatus != AppointmentStatus.Cancelled)
            .Select(appointment =>
            {
                var start = appointment.Start.ToUniversalTime();
                var end = appointment.End.ToUniversalTime();
                if (end <= start) end = start.Add(TimeSpan.FromMinutes(DefaultDurationMinutes));
                return (Start: start, End: end);
            })
            .ToList();

        // Blocks are the provider's own time off. Kept as intervals and filtered to the ones that could matter
        // at all, so a provider with years of history does not re-scan every past block per candidate slot.
        var windowEndUtc = nowUtc.AddDays(window + 1);
        var timeOff = (blocks ?? [])
            .Where(block => block.DescribesAnInterval
                            && block.End.ToUniversalTime() > nowUtc
                            && block.Start.ToUniversalTime() < windowEndUtc)
            .Select(block => (Start: block.Start.ToUniversalTime(), End: block.End.ToUniversalTime()))
            .ToList();

        var zone = ResolveZone(provider.TimeZoneId);

        var slots = new List<DateTime>();

        // Days are walked in the PROVIDER'S calendar, not UTC's: at UTC-6 the provider's "today" starts
        // six hours after the UTC date rolls over, so iterating UTC dates would offer a window shifted off
        // their actual working day.
        var firstLocalDate = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone).Date;

        for (var offset = 0; offset < window; offset++)
        {
            var localDate = firstLocalDate.AddDays(offset);

            // Hours are resolved PER DAY, on the provider's own weekday — a provider who works Saturday
            // mornings and not at all on Sunday has two different answers inside one window.
            var hours = ResolveHours(provider, localDate.DayOfWeek);
            if (hours is null) continue;

            var (openingHour, closingHour) = hours.Value;

            for (var hour = openingHour; hour < closingHour; hour++)
            {
                var localStart = new DateTime(
                    localDate.Year, localDate.Month, localDate.Day, hour, 0, 0, DateTimeKind.Unspecified);

                // A session must finish by closing time, measured on the same local clock -- so a DST
                // shift cannot silently stretch or shorten the working day.
                if (localStart.AddMinutes(durationMinutes > 0 ? durationMinutes : DefaultDurationMinutes)
                    > localDate.AddHours(closingHour))
                    continue;

                if (!TryToUtc(localStart, zone, out var slot)) continue;
                if (slot <= nowUtc) continue;

                // Day-off is judged on the provider's local date too, for the same reason as above.
                if (daysOff.Contains(TimeZoneInfo.ConvertTimeFromUtc(slot, zone).Date)) continue;

                // Half-open intervals: an appointment ending exactly when a slot starts is not a clash,
                // so back-to-back sessions remain bookable. Time off is compared the same way, so a block
                // ending at noon leaves noon bookable.
                var slotEnd = slot + duration;
                if (busy.Any(b => slot < b.End && b.Start < slotEnd)) continue;
                if (timeOff.Any(b => slot < b.End && b.Start < slotEnd)) continue;

                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>
    /// The provider's working window for one weekday, or <c>null</c> when they are closed that day.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three tiers, most specific first: an entry in <see cref="ProviderEntity.WorkWeek"/> for this weekday, then
    /// the single <see cref="ProviderEntity.WorkDayStartHour"/>/<see cref="ProviderEntity.WorkDayEndHour"/> pair,
    /// then the default. The middle tier is what keeps every provider stored before per-weekday hours existed
    /// bookable on exactly the hours they had.
    /// </para>
    /// <para>
    /// <b>Only an explicitly closed day returns <c>null</c>.</b> An entry with unusable hours falls through to
    /// the fallback rather than closing the day, because "0 to 0" is far more likely to be a bad write than a
    /// deliberate closure — and a provider silently unbookable is worse than one bookable on standard hours.
    /// Saying "closed" takes the flag.
    /// </para>
    /// </remarks>
    internal static (int OpeningHour, int ClosingHour)? ResolveHours(ProviderEntity provider, DayOfWeek day)
    {
        // First wins on a duplicate day, so a document written twice cannot yield two windows for one weekday.
        var forDay = provider.WorkWeek?.FirstOrDefault(entry => entry.Day == day);

        if (forDay is not null)
        {
            if (forDay.IsClosed) return null;
            if (forDay.DescribesAWindow) return (forDay.StartHour!.Value, forDay.EndHour!.Value);
        }

        return ResolveHours(provider);
    }

    /// <summary>
    /// The provider's working window, or <see cref="DefaultOpeningHour"/>–<see cref="DefaultClosingHour"/>
    /// when they have not set one.
    /// </summary>
    /// <remarks>
    /// A stored pair that cannot describe a usable window falls back to the default rather than producing
    /// no slots at all: a provider silently unbookable is worse than one bookable on standard hours, and
    /// the API rejects such a pair on the way in, so reaching here means the data predates that check or
    /// was written around it.
    /// </remarks>
    internal static (int OpeningHour, int ClosingHour) ResolveHours(ProviderEntity provider)
    {
        var start = provider.WorkDayStartHour;
        var end = provider.WorkDayEndHour;

        var usable = start is >= 0 and <= 23
            && end is >= 1 and <= 24
            && start < end;

        return usable
            ? (start!.Value, end!.Value)
            : (DefaultOpeningHour, DefaultClosingHour);
    }

    /// <summary>
    /// The provider's zone, or UTC when it is missing or unknown to this machine.
    /// </summary>
    /// <remarks>
    /// An id this host cannot resolve must not take the whole calendar down — a provider whose zone was
    /// recorded on a platform with a different tz database still needs to be bookable, just on UTC hours.
    /// </remarks>
    internal static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Converts a wall-clock time in <paramref name="zone"/> to a UTC instant.
    /// </summary>
    /// <returns>
    /// <c>false</c> when that wall-clock time does not exist — the hour skipped by spring-forward. Such a
    /// slot is dropped rather than shifted, because offering a start time that cannot occur would fail at
    /// booking. An AMBIGUOUS time (the hour autumn repeats) is resolved to a single instant, so a
    /// transition day yields one slot for that hour rather than two identical-looking ones.
    /// </returns>
    private static bool TryToUtc(DateTime localStart, TimeZoneInfo zone, out DateTime utc)
    {
        if (zone.IsInvalidTime(localStart))
        {
            utc = default;
            return false;
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), zone);
        return true;
    }
}
