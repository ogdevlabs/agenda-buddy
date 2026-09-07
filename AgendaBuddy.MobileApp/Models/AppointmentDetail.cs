using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Models;

public class AppointmentDetail
{
    public string Id { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Session length as booked. Null for appointments made before services were selectable.</summary>
    public int? ServiceDurationMinutes { get; set; }
    public string CustomerNotes { get; set; } = string.Empty;
    public bool HasNotes => !string.IsNullOrWhiteSpace(CustomerNotes);

    /// <summary>
    /// The new start somebody has proposed, on this device's clock. Set only while
    /// <see cref="AppointmentStatus.RescheduleRequested"/>.
    /// </summary>
    /// <remarks>
    /// Held alongside <see cref="ScheduledAt"/>, which is deliberately still where the session IS. A proposal is
    /// not an agreement, so a pending time must never be drawn as though it were the appointment — the reader
    /// still has to be somewhere at the original time until the other party answers.
    /// </remarks>
    public DateTime? ProposedStart { get; set; }

    /// <summary>Who proposed it. Compared against the reader's own email to decide who owes the answer.</summary>
    public string ProposedBy { get; set; } = string.Empty;

    /// <summary>Where this session was before the most recent completed reschedule, on this device's clock.</summary>
    public DateTime? PreviousStart { get; set; }

    /// <summary>Whether a proposal is outstanding, so somebody owes an answer.</summary>
    public bool HasPendingReschedule =>
        Status == AppointmentStatus.RescheduleRequested && ProposedStart.HasValue;

    /// <summary>
    /// The last moment a CUSTOMER may still cancel, on this device's clock.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>AppointmentEntity.CustomerCancellationDeadlineUtc</c>. Computed client-side so the deadline can
    /// be SHOWN while it is still ahead — the server remains authoritative, and its refusal is what actually
    /// stops a late cancellation. Clamped for the same reason the server's is: an appointment with no scheduled
    /// time would underflow.
    /// </remarks>
    public DateTime CustomerCancellationDeadline
    {
        get
        {
            var notice = TimeSpan.FromHours(AppointmentEntity.CustomerCancellationNoticeHours);
            return ScheduledAt - DateTime.MinValue < notice ? DateTime.MinValue : ScheduledAt - notice;
        }
    }
}
