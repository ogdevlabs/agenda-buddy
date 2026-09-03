namespace AgendaBuddy.IntegrationTests.Harness;

/// <summary>
/// Appointment times for tests, expressed relative to now rather than as calendar dates.
/// </summary>
/// <remarks>
/// <c>BookingAppointmentCommandHandler</c> rejects a <c>Start</c> at or before the current instant, so a
/// hardcoded date is a time bomb: every booking test written against <c>2026-09-01</c> passed until that
/// date arrived and then failed with a 400 for a reason that has nothing to do with what it was asserting.
/// Anchoring to <see cref="DateTime.UtcNow"/> keeps the intent ("a slot in the future") true on every run.
/// </remarks>
internal static class FutureSlot
{
    /// <summary>
    /// A UTC instant <paramref name="daysAhead"/> whole days from today at <paramref name="hour"/>:00.
    /// Distinct <paramref name="daysAhead"/> values give non-overlapping slots.
    /// </summary>
    public static DateTime Start(int daysAhead = 7, int hour = 10) =>
        DateTime.UtcNow.Date.AddDays(daysAhead).AddHours(hour);
}
