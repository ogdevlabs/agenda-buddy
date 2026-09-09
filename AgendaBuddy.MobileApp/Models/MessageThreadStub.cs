using AgendaBuddy.Library.Avatars;

using AgendaBuddy.MobileApp.Resources.Strings;

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

    /// <summary>
    /// The unread count as the badge draws it, capped at <c>99+</c>.
    /// </summary>
    /// <remarks>
    /// A raw count is unbounded and the badge is a small pill, so a busy thread rendered a clipped "252". The
    /// exact number past 99 tells the reader nothing they act on differently — the same reason the header's
    /// notification bell caps.
    /// </remarks>
    public string UnreadLabel => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

    private static string FormatTimeAgo(DateTime dt) => RuntimeText.RelativeTime(dt, DateTime.Now);
}
