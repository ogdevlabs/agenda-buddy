using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.Library.Entities;

namespace AgendaBuddy.MobileApp.Models;

public partial class AppointmentSummary : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ProviderEmail { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Session length as booked. Null for appointments made before services were selectable.</summary>
    public int? ServiceDurationMinutes { get; set; }

    /// <summary>"45 min", or a dash when the appointment predates service selection.</summary>
    public string DurationLabel => ServiceDurationMinutes is null ? "—" : $"{ServiceDurationMinutes} min";
    public string CustomerNotes { get; set; } = string.Empty;

    /// <summary>The counterpart this session is with, from the reader's side.</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>
    /// The counterpart's assigned avatar id, filled from the contact directory rather than the appointment wire.
    /// </summary>
    public string ContactAvatarId { get; set; } = string.Empty;

    /// <summary>The image to bind, resolved the same way on every surface that shows a person.</summary>
    public string AvatarAsset => Infrastructure.AvatarSource.For(ContactAvatarId, ContactEmail);

    [ObservableProperty]
    private bool _isExpanded;

    public bool IsPast => ScheduledAt < DateTime.Now;
}
