using AgendaBuddy.Library.Showcase;
using Xunit;

namespace AgendaBuddy.Library.Tests.Showcase;

public class ShowcaseRulesTest
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Hello  ", "Hello")]
    public void Clean_TrimsAndTreatsEmptyAsNull(string? input, string? expected) =>
        Assert.Equal(expected, ShowcaseRules.Clean(input));

    [Fact]
    public void FitsWithin_NullAlwaysFits() => Assert.True(ShowcaseRules.FitsWithin(null, 0));

    [Fact]
    public void FitsWithin_CountsAtTheBoundary()
    {
        Assert.True(ShowcaseRules.FitsWithin(new string('a', ShowcaseRules.MaxTagline), ShowcaseRules.MaxTagline));
        Assert.False(ShowcaseRules.FitsWithin(new string('a', ShowcaseRules.MaxTagline + 1), ShowcaseRules.MaxTagline));
    }

    [Fact]
    public void FitsWithin_CountsGraphemesNotUtf16Units()
    {
        // Each family emoji is several UTF-16 units but one visible character.
        var emoji = string.Concat(Enumerable.Repeat("👨‍👩‍👧‍👦", 10));
        var accented = string.Concat(Enumerable.Repeat("é", 10));

        Assert.True(emoji.Length > 10);
        Assert.True(ShowcaseRules.FitsWithin(emoji, 10));
        Assert.False(ShowcaseRules.FitsWithin(emoji, 9));
        Assert.True(ShowcaseRules.FitsWithin(accented, 10));
    }

    [Fact]
    public void IsHash_AcceptsLowercaseSha256Hex()
    {
        Assert.True(ShowcaseRules.IsHash(new string('a', 64)));
        Assert.True(ShowcaseRules.IsHash("0123456789abcdef" + new string('0', 48)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void IsHash_RejectsWrongLengths(string? value) => Assert.False(ShowcaseRules.IsHash(value));

    [Fact]
    public void IsHash_RejectsUppercaseAndNonHexAndPathCharacters()
    {
        Assert.False(ShowcaseRules.IsHash(new string('A', 64)));
        Assert.False(ShowcaseRules.IsHash(new string('g', 64)));
        Assert.False(ShowcaseRules.IsHash(new string('a', 62) + ".."));
        Assert.False(ShowcaseRules.IsHash(new string('a', 63)));
        Assert.False(ShowcaseRules.IsHash(new string('a', 65)));
    }

    [Theory]
    [InlineData("scan", "scan")]
    [InlineData(" SCAN ", "scan")]
    [InlineData("booking", "booking")]
    [InlineData("twitter", "directory")]
    [InlineData(null, "directory")]
    [InlineData("", "directory")]
    public void Sources_Normalise_FallsBackToDirectory(string? input, string expected) =>
        Assert.Equal(expected, ShowcaseSources.Normalise(input));

    [Fact]
    public void ReportReasons_IncludeOther() => Assert.Contains("other", ShowcaseReportReasons.All);
}
