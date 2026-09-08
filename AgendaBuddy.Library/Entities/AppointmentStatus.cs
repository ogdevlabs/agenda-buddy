namespace AgendaBuddy.Library.Entities;

/// <summary>
/// An appointment's lifecycle state.
/// </summary>
/// <remarks>
/// <b>APPEND-ONLY, and the integers are pinned deliberately.</b> The numeric value is what MongoDB stores, so
/// reordering or inserting a member silently reinterprets every existing appointment — a stored <c>4</c> would
/// stop meaning <see cref="Cancelled"/>. They were previously implicit; they are written out so that a member
/// added in the wrong place is a visible mistake rather than a data migration nobody noticed.
/// </remarks>
public enum AppointmentStatus
{
    [Description("Appointment Requested")] Requested = 0,
    [Description("Appointment Booked")] Booked = 1,
    [Description("Appointment Completed")] Completed = 2,

    /// <summary>
    /// Never persisted — only ever produced on a Calendar projection. Kept because its integer is spent.
    /// </summary>
    [Description("Appointment Confirmed")] Confirmed = 3,

    [Description("Appointment Cancelled")] Cancelled = 4,

    /// <summary>
    /// One party has proposed a new time and the other has not answered yet. The appointment still holds its
    /// original <c>Start</c>/<c>End</c> — the proposal lives alongside it in <c>proposed_start</c>, so a
    /// pending request cannot be mistaken for an agreed change.
    /// </summary>
    [Description("Reschedule Requested")] RescheduleRequested = 5
}
