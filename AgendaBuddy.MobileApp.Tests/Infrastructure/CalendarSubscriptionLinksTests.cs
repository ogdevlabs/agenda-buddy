using AgendaBuddy.MobileApp.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class CalendarSubscriptionLinksTests
{
    private const string Feed = "https://gateway.example.com/api/v1/calendar/feed/abc_DEF-123.ics";

    [Theory]
    [InlineData(Feed, "webcal://gateway.example.com/api/v1/calendar/feed/abc_DEF-123.ics")]
    [InlineData("http://localhost:6080/api/v1/calendar/feed/t.ics", "webcal://localhost:6080/api/v1/calendar/feed/t.ics")]
    [InlineData("gateway.example.com/feed.ics", "webcal://gateway.example.com/feed.ics")]
    public void WebcalReplacesOnlyTheScheme(string feed, string expected) =>
        Assert.Equal(expected, CalendarSubscriptionLinks.Webcal(feed));

    [Fact]
    public void GoogleSubscribesThroughItsOwnPageWithTheEscapedWebcalLink()
    {
        var link = CalendarSubscriptionLinks.Google(Feed);

        Assert.StartsWith("https://calendar.google.com/calendar/r?cid=", link);
        var cid = Uri.UnescapeDataString(link["https://calendar.google.com/calendar/r?cid=".Length..]);
        Assert.Equal(CalendarSubscriptionLinks.Webcal(Feed), cid);
        Assert.DoesNotContain("://gateway", link["https://".Length..]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ABlankLinkIsRefused(string feed) =>
        Assert.ThrowsAny<ArgumentException>(() => CalendarSubscriptionLinks.Webcal(feed));
}
