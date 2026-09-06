using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.Library.Avatars;

namespace AgendaBuddy.MobileApp.Models;

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
    /// <summary>
    /// The image to draw for the other party.
    /// </summary>
    /// <remarks>
    /// Derived from their address, not looked up: a thread stub is built from messages and never carries the
    /// counterparty's profile, so there is no assigned avatar to read. The derivation is stable, which is what
    /// makes the same person show the same mark here and in the contacts list.
    /// </remarks>
    public string AvatarAsset => $"{AvatarCatalog.Deterministic(OtherPartyEmail)}.png";
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
