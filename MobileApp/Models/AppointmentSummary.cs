using Library.Entities;

namespace MobileApp.Models;

/// <summary>Lightweight DTO for displaying appointment summaries in lists and the dashboard.</summary>
public class AppointmentSummary
{
    public string Id { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string ServiceId { get; set; } = string.Empty;
}
