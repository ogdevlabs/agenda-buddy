using CommunityToolkit.Mvvm.ComponentModel;

namespace MobileApp.Models;

public partial class MessageThreadStub : ObservableObject
{
    public string ThreadId { get; set; } = string.Empty;
    public string OtherPartyEmail { get; set; } = string.Empty;
    public string LastMessageBody { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }

    [ObservableProperty]
    private bool _isExpanded;

    public string SenderName => OtherPartyEmail.Split('@')[0].Replace(".", " ");
    public string Initial => string.IsNullOrEmpty(OtherPartyEmail) ? "?" : OtherPartyEmail[0].ToString().ToUpper();
    public string TimeAgo => FormatTimeAgo(LastMessageAt);
    public bool HasUnread => UnreadCount > 0;

    private static string FormatTimeAgo(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }
}
