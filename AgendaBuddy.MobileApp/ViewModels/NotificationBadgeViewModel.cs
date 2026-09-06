using CommunityToolkit.Mvvm.ComponentModel;
using AgendaBuddy.MobileApp.Services;

namespace AgendaBuddy.MobileApp.ViewModels;

/// <summary>
/// The unread count, shared by every surface that shows a badge.
/// </summary>
/// <remarks>
/// <para>
/// A <b>singleton</b>, for the same reason <c>BrandHeaderViewModel</c> is one: two surfaces showing the same
/// number must not each hold their own copy, or opening Notifications clears one badge and leaves the other
/// stale until the page it lives on is rebuilt.
/// </para>
/// <para>
/// It reads <c>unread-count</c> rather than counting a fetched list, so a badge costs one small request and
/// not the whole inbox. Decrementing locally on a mark-read is deliberate: the alternative is a round trip per
/// tap, and the count is re-read from the server on the next refresh anyway.
/// </para>
/// </remarks>
public partial class NotificationBadgeViewModel : ObservableObject
{
    private readonly INotificationApiService _notificationApiService;

    [ObservableProperty]
    private long _unreadCount;

    public NotificationBadgeViewModel(INotificationApiService notificationApiService)
    {
        _notificationApiService = notificationApiService;
    }

    public bool HasUnread => UnreadCount > 0;

    /// <summary>
    /// The count as text. Capped at "99+" because a three-digit number does not fit a badge, and the
    /// difference between 100 and 400 unread does not change what the reader does next.
    /// </summary>
    public string BadgeText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

    /// <summary>
    /// Re-reads the count from the server. Never throws — a badge must not break the screen it is on.
    /// </summary>
    /// <remarks>
    /// A count the server could not supply leaves the previous value alone rather than overwriting it with
    /// zero. The old behaviour meant one failed request cleared a badge that had unread notifications behind
    /// it, and this method is called on every navigation, so a single blip anywhere silenced the only signal
    /// most screens carry.
    /// </remarks>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var count = await _notificationApiService.GetUnreadCountAsync(ct);
        if (count.HasValue)
            UnreadCount = Math.Max(0, count.Value);
    }

    /// <summary>Applies a known-good local change without a round trip. Floors at zero.</summary>
    public void Decrement(long by = 1) => UnreadCount = Math.Max(0, UnreadCount - by);

    /// <summary>
    /// Counts an arrival the client learnt about directly, before any request confirms it.
    /// </summary>
    /// <remarks>
    /// A push arriving *is* the authoritative news that there is one more unread, so the badge moves
    /// immediately — waiting for a round trip would leave the count behind the banner that just announced it.
    /// The next <see cref="RefreshAsync"/> reconciles.
    /// </remarks>
    public void Increment(long by = 1) => UnreadCount += Math.Max(0, by);

    /// <summary>Sets the count directly, for a caller that has just learnt it authoritatively.</summary>
    public void Set(long count) => UnreadCount = Math.Max(0, count);

    /// <summary>Clears the count. Sign-out, so the next account does not inherit the previous one's badge.</summary>
    public void Clear() => UnreadCount = 0;

    partial void OnUnreadCountChanged(long value)
    {
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(BadgeText));
    }
}
