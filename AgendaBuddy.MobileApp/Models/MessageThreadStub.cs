using AgendaBuddy.Library.Avatars;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// One conversation, as the inbox list draws it. Read-only: a row's single action is to open the thread, and
/// the thread is what marks its messages read against the server.
/// </summary>
public class MessageThreadStub
{
    public string ThreadId { get; set; } = string.Empty;
    public string OtherPartyEmail { get; set; } = string.Empty;
    public string LastMessageBody { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public int UnreadCount { get; set; }

    public string SenderName => OtherPartyEmail.Split('@')[0].Replace(".", " ");
    /// <summary>
    /// The image to draw for the other party.
    /// </summary>
    /// <remarks>
    /// Derived from their address, not looked up: a thread stub is built from messages and never carries the
    /// counterparty's profile, so there is no assigned avatar to read. The derivation is stable, which is what
    /// makes the same person show the same mark here and in the contacts list.
    /// </remarks>
    /// <summary>
    /// The counterpart's assigned avatar id, when a directory read supplied one.
    /// </summary>
    /// <remarks>
    /// The message wire carries participant emails and nothing else, so a thread only knows the assigned mark if
    /// something filled it in. Empty is the normal case and falls back to the deterministic derivation.
    /// </remarks>
    public string OtherPartyAvatarId { get; set; } = string.Empty;

    public string AvatarAsset =>
        Infrastructure.AvatarSource.For(OtherPartyAvatarId, OtherPartyEmail);
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
