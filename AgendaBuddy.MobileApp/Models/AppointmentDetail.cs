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
}
