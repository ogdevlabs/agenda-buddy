using AgendaBuddy.Library.Calendar;
using Xunit;

namespace AgendaBuddy.Library.Tests.Calendar;

public class CalendarFeedTokenTest
{
    [Fact]
    public void NewTokensAreWellFormedAndDistinct()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => CalendarFeedToken.New()).ToList();

        Assert.All(tokens, token => Assert.True(CalendarFeedToken.IsWellFormed(token)));
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void MalformedTokensAreRejected(string? token) => Assert.False(CalendarFeedToken.IsWellFormed(token));

    [Fact]
    public void HashIsStableAndDoesNotContainTheToken()
    {
        var token = CalendarFeedToken.New();

        Assert.Equal(CalendarFeedToken.Hash(token), CalendarFeedToken.Hash(token));
        Assert.Equal(64, CalendarFeedToken.Hash(token).Length);
        Assert.DoesNotContain(token, CalendarFeedToken.Hash(token));
    }
}
