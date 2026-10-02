namespace AgendaBuddy.MobileApp.Infrastructure;

/// <summary>
/// The two ways a phone's calendar subscribes to a feed. Apple Calendar opens a <c>webcal://</c> link and offers to
/// subscribe; Google Calendar, which is what Android's calendar shows, subscribes through its own web page given
/// that link as <c>cid</c>. Neither needs a calendar permission on the device.
/// </summary>
public static class CalendarSubscriptionLinks
{
    private const string GoogleSubscribeBase = "https://calendar.google.com/calendar/r?cid=";

    /// <summary>The feed's address with its scheme replaced by <c>webcal</c>; anything else about it is kept.</summary>
    public static string Webcal(string feedUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedUrl);

        var separator = feedUrl.IndexOf("://", StringComparison.Ordinal);
        return separator < 0 ? "webcal://" + feedUrl : "webcal" + feedUrl[separator..];
    }

    public static string Google(string feedUrl) => GoogleSubscribeBase + Uri.EscapeDataString(Webcal(feedUrl));
}
