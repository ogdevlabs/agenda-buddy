using Library.Entities;

namespace MobileApp.Models;

/// <summary>DTO for the appointment detail page (US-003). Kept separate from
/// <see cref="AppointmentSummary"/> so list and detail views can evolve independently.</summary>
public class AppointmentDetail
{
    public string Id { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string ServiceId { get; set; } = string.Empty;
}
