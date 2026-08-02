using Library.Entities;

namespace MobileApp.Models;

public class AppointmentDetail
{
    public string Id { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string CustomerNotes { get; set; } = string.Empty;
    public bool HasNotes => !string.IsNullOrWhiteSpace(CustomerNotes);
}
