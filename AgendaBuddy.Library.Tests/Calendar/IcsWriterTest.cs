using System.Text;
using AgendaBuddy.Library.Calendar;
using Xunit;

namespace AgendaBuddy.Library.Tests.Calendar;

public class IcsWriterTest
{
    private static readonly DateTime Stamp = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EveryLineEndsInCrlf()
    {
        var ics = IcsWriter.Write("AgendaMe", [Event("Yoga")], Stamp);

        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void CalendarCarriesNameAndRefreshHint()
    {
        var ics = IcsWriter.Write("AgendaMe", [], Stamp);

        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics);
        Assert.Contains("X-WR-CALNAME:AgendaMe\r\n", ics);
        Assert.Contains("REFRESH-INTERVAL;VALUE=DURATION:PT15M\r\n", ics);
        Assert.Contains("X-PUBLISHED-TTL:PT15M\r\n", ics);
    }

    [Fact]
    public void EventTimesAreUtcBasicFormat()
    {
        var ics = IcsWriter.Write("AgendaMe", [Event("Yoga")], Stamp);

        Assert.Contains("DTSTART:20261002T150000Z\r\n", ics);
        Assert.Contains("DTEND:20261002T160000Z\r\n", ics);
        Assert.Contains("DTSTAMP:20261001T120000Z\r\n", ics);
    }

    [Theory]
    [InlineData("a,b", "a\\,b")]
    [InlineData("a;b", "a\\;b")]
    [InlineData("a\\b", "a\\\\b")]
    [InlineData("a\nb", "a\\nb")]
    [InlineData("a\r\nb", "a\\nb")]
    public void TextIsEscaped(string raw, string escaped) => Assert.Equal(escaped, IcsWriter.EscapeText(raw));

    [Fact]
    public void LongLinesFoldAtSeventyFiveOctets()
    {
        var folded = IcsWriter.Fold("SUMMARY:" + new string('x', 200));

        foreach (var line in folded.Split("\r\n"))
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75);
        Assert.Equal("SUMMARY:" + new string('x', 200), folded.Replace("\r\n ", string.Empty));
    }

    [Fact]
    public void FoldingNeverSplitsAMultiByteCharacter()
    {
        var content = "SUMMARY:" + string.Concat(Enumerable.Repeat("Sesión ", 30));
        var folded = IcsWriter.Fold(content);

        foreach (var line in folded.Split("\r\n"))
        {
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75);
            Assert.DoesNotContain('�', Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(line)));
        }
        Assert.Equal(content, folded.Replace("\r\n ", string.Empty));
    }

    [Theory]
    [InlineData(IcsEventStatus.Tentative, "STATUS:TENTATIVE", "TRANSP:OPAQUE")]
    [InlineData(IcsEventStatus.Confirmed, "STATUS:CONFIRMED", "TRANSP:OPAQUE")]
    [InlineData(IcsEventStatus.Cancelled, "STATUS:CANCELLED", "TRANSP:TRANSPARENT")]
    public void StatusAndTransparencyFollowTheEvent(IcsEventStatus status, string statusLine, string transpLine)
    {
        var ics = IcsWriter.Write("AgendaMe", [Event("Yoga") with { Status = status }], Stamp);

        Assert.Contains(statusLine + "\r\n", ics);
        Assert.Contains(transpLine + "\r\n", ics);
    }

    private static IcsEvent Event(string summary) => new(
        "id-1@agendame",
        new DateTime(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc),
        summary,
        "Managed in AgendaMe.",
        IcsEventStatus.Confirmed);
}
