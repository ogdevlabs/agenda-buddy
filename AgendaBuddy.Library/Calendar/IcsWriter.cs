using System.Globalization;
using System.Text;

namespace AgendaBuddy.Library.Calendar;

public enum IcsEventStatus
{
    Tentative,
    Confirmed,
    Cancelled
}

public sealed record IcsEvent(
    string Uid,
    DateTime StartUtc,
    DateTime EndUtc,
    string Summary,
    string Description,
    IcsEventStatus Status);

/// <summary>
/// Serialises events as an RFC 5545 VCALENDAR: CRLF line endings, TEXT escaping, and content lines folded at 75
/// octets without splitting a UTF-8 sequence. Times are always UTC (<c>Z</c> form), so no VTIMEZONE is needed and
/// every subscriber renders the instant in its own zone.
/// </summary>
public static class IcsWriter
{
    private const int MaxLineOctets = 75;

    /// <summary>
    /// How often a subscriber is asked to re-fetch. Advisory: Apple Calendar honours it, Google Calendar decides for
    /// itself.
    /// </summary>
    public const string RefreshInterval = "PT15M";

    public static string Write(string calendarName, IEnumerable<IcsEvent> events, DateTime stampUtc)
    {
        var builder = new StringBuilder();
        Line(builder, "BEGIN:VCALENDAR");
        Line(builder, "VERSION:2.0");
        Line(builder, "PRODID:-//AgendaMe//Calendar Feed//EN");
        Line(builder, "CALSCALE:GREGORIAN");
        Line(builder, "METHOD:PUBLISH");
        Line(builder, "X-WR-CALNAME:" + EscapeText(calendarName));
        Line(builder, "REFRESH-INTERVAL;VALUE=DURATION:" + RefreshInterval);
        Line(builder, "X-PUBLISHED-TTL:" + RefreshInterval);

        var stamp = FormatUtc(stampUtc);
        foreach (var calendarEvent in events)
        {
            Line(builder, "BEGIN:VEVENT");
            Line(builder, "UID:" + EscapeText(calendarEvent.Uid));
            Line(builder, "DTSTAMP:" + stamp);
            Line(builder, "DTSTART:" + FormatUtc(calendarEvent.StartUtc));
            Line(builder, "DTEND:" + FormatUtc(calendarEvent.EndUtc));
            Line(builder, "SUMMARY:" + EscapeText(calendarEvent.Summary));
            Line(builder, "DESCRIPTION:" + EscapeText(calendarEvent.Description));
            Line(builder, "STATUS:" + StatusValue(calendarEvent.Status));
            Line(builder, "TRANSP:" + (calendarEvent.Status == IcsEventStatus.Cancelled ? "TRANSPARENT" : "OPAQUE"));
            Line(builder, "END:VEVENT");
        }

        Line(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    public static string FormatUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>RFC 5545 §3.3.11: backslash, semicolon and comma are escaped; every line break becomes <c>\n</c>.</summary>
    public static string EscapeText(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>
    /// Folds one content line into 75-octet physical lines, each continuation starting with a single space. Counted in
    /// UTF-8 octets and cut only on a character boundary, so "Sesión" never splits inside the "ó".
    /// </summary>
    public static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= MaxLineOctets)
            return line;

        var folded = new StringBuilder();
        var octets = 0;
        var limit = MaxLineOctets;
        var enumerator = StringInfo.GetTextElementEnumerator(line);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var size = Encoding.UTF8.GetByteCount(element);
            if (octets + size > limit)
            {
                folded.Append("\r\n ");
                octets = 0;
                // The leading space of a continuation line counts toward its 75 octets.
                limit = MaxLineOctets - 1;
            }

            folded.Append(element);
            octets += size;
        }

        return folded.ToString();
    }

    private static void Line(StringBuilder builder, string content) => builder.Append(Fold(content)).Append("\r\n");

    private static string StatusValue(IcsEventStatus status) => status switch
    {
        IcsEventStatus.Tentative => "TENTATIVE",
        IcsEventStatus.Cancelled => "CANCELLED",
        _ => "CONFIRMED"
    };
}
