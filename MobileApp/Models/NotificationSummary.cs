using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.Library.Entities;

namespace MobileApp.Models;

public partial class NotificationSummary : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public NotificationType NotificationType { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }

    [ObservableProperty]
    private bool _isExpanded;

    public string TimeAgo => FormatTimeAgo(CreatedAt);
    public string TypeLabel => NotificationType switch
    {
        NotificationType.AppointmentBooked => "Booked",
        NotificationType.AppointmentUpdated => "Updated",
        NotificationType.AppointmentCancelled => "Cancelled",
        NotificationType.AppointmentCompleted => "Completed",
        _ => "Info"
    };

    private static string FormatTimeAgo(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }
}
