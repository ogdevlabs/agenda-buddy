using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// The unread badge is the only notification signal most screens carry, so a badge that says zero when it does
/// not know is a notification nobody is told about.
/// </summary>
public class NotificationBadgeViewModelTests
{
    private static NotificationBadgeViewModel Badge(long? serverCount, out Mock<INotificationApiService> api)
    {
        api = new Mock<INotificationApiService>();
        api.Setup(a => a.GetUnreadCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(serverCount);
        return new NotificationBadgeViewModel(api.Object);
    }

    [Fact]
    public async Task Refresh_TakesTheServersCount()
    {
        var badge = Badge(7, out _);

        await badge.RefreshAsync();

        Assert.Equal(7, badge.UnreadCount);
        Assert.True(badge.HasUnread);
        Assert.Equal("7", badge.BadgeText);
    }

    /// <summary>
    /// A count the server could not supply leaves the previous value alone. Overwriting it with zero meant one
    /// failed request cleared a badge with real unread notifications behind it — and this runs on every
    /// navigation, so a single blip anywhere silenced the signal until something else refreshed it.
    /// </summary>
    [Fact]
    public async Task Refresh_WhenTheCountCannotBeRead_KeepsWhatItAlreadyHad()
    {
        var badge = Badge(null, out _);
        badge.Set(4);

        await badge.RefreshAsync();

        Assert.Equal(4, badge.UnreadCount);
        Assert.True(badge.HasUnread);
    }

    // A real zero still clears it: "nothing unread" is an answer, and the badge must not stick on a stale count.
    [Fact]
    public async Task Refresh_WhenTheServerReportsNothingUnread_ClearsTheBadge()
    {
        var badge = Badge(0, out _);
        badge.Set(4);

        await badge.RefreshAsync();

        Assert.Equal(0, badge.UnreadCount);
        Assert.False(badge.HasUnread);
    }

    /// <summary>
    /// A push arriving is itself the news that there is one more unread, so the badge moves immediately rather
    /// than waiting for a round trip to confirm what the banner has already announced.
    /// </summary>
    [Fact]
    public void Increment_CountsAnArrivalWithoutARoundTrip()
    {
        var badge = Badge(0, out var api);

        badge.Increment();
        badge.Increment();

        Assert.Equal(2, badge.UnreadCount);
        api.Verify(a => a.GetUnreadCountAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Decrement_FloorsAtZero()
    {
        var badge = Badge(0, out _);

        badge.Decrement();

        Assert.Equal(0, badge.UnreadCount);
    }

    // A three-digit number does not fit a badge, and 100 vs 400 unread does not change what the reader does next.
    [Fact]
    public void ABadgeOverNinetyNineIsAbbreviated()
    {
        var badge = Badge(0, out _);
        badge.Set(140);

        Assert.Equal("99+", badge.BadgeText);
    }

    // Sign-out, so the next account does not inherit the previous one's badge.
    [Fact]
    public void Clear_EmptiesTheBadge()
    {
        var badge = Badge(0, out _);
        badge.Set(9);

        badge.Clear();

        Assert.Equal(0, badge.UnreadCount);
        Assert.False(badge.HasUnread);
    }
}
