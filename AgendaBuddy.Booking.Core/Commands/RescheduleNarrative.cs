namespace AgendaBuddy.Booking.Core.Commands;

/// <summary>
/// The wording of every reschedule notification, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Shared because a reschedule produces four different notifications across three handlers — moved, proposed,
/// approved, declined — and they have to agree on how a time is written. Four independently-worded copies is
/// four chances for one of them to name a time in a different format, or to omit the old one.
/// </para>
/// <para>
/// <b>Every message names BOTH times where two exist.</b> "Your session has moved to Tuesday 3pm" leaves the
/// reader unsure whether they misremembered the old time; naming both makes the change checkable. Times are
/// rendered with <c>ToLocalTime()</c> — the SERVER's local zone, which is the same compromise every other
/// notification body in this solution already makes, since a notification body is composed once and read by two
/// people who may be in different zones. The in-app row is the accurate surface: it carries the appointment
/// identifier, so the client renders the real times on the device's own clock.
/// </para>
/// </remarks>
internal static class RescheduleNarrative
{
    /// <summary>A provider moved the session outright.</summary>
    public static string Moved(AppointmentEntity appointment, DateTime previousStartUtc, string counterparty) =>
        $"{Service(appointment)} with {counterparty} moved from {When(previousStartUtc)} to "
        + $"{When(appointment.Start)}.";

    /// <summary>Somebody proposed a new time and the reader owes an answer.</summary>
    public static string Proposed(AppointmentEntity appointment, DateTime proposedStartUtc, string counterparty) =>
        $"{counterparty} asked to move {Service(appointment).ToLowerInvariant()} from {When(appointment.Start)} "
        + $"to {When(proposedStartUtc)}. Approve or decline it.";

    /// <summary>A proposal was accepted and the session has moved.</summary>
    public static string Approved(AppointmentEntity appointment, DateTime previousStartUtc, string counterparty) =>
        $"{counterparty} approved the new time. {Service(appointment)} moved from {When(previousStartUtc)} to "
        + $"{When(appointment.Start)}.";

    /// <summary>A proposal was refused and the session stands where it was.</summary>
    public static string Declined(AppointmentEntity appointment, string counterparty) =>
        $"{counterparty} declined the new time. {Service(appointment)} is still on "
        + $"{When(appointment.Start)}.";

    private static string Service(AppointmentEntity appointment) =>
        string.IsNullOrWhiteSpace(appointment.ServiceName) ? "A session" : appointment.ServiceName;

    private static string When(DateTime utc) => $"{utc.ToLocalTime():dddd d MMMM 'at' h:mm tt}";
}
