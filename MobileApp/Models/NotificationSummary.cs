using Library.Entities;

namespace MobileApp.Models;

/// <summary>Lightweight DTO for displaying notification summaries in the notifications list.</summary>
public class NotificationSummary
{
    public string Id { get; set; } = string.Empty;
    public NotificationType NotificationType { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
