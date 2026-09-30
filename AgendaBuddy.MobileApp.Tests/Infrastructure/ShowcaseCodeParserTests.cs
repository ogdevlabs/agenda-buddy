using AgendaBuddy.MobileApp.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Infrastructure;

public class ShowcaseCodeParserTests
{
    [Theory]
    [InlineData("https://agendame.app/api/v1/go/K7Q2X9", "K7Q2X9")]
    [InlineData("http://localhost:6080/api/v1/go/K7Q2X9", "K7Q2X9")]
    [InlineData("https://gateway.example.net/api/v1/go/k7q2x9/", "K7Q2X9")]
    [InlineData("  https://any.host/prefix/api/v1/go/K7Q2X9?utm=1  ", "K7Q2X9")]
    public void AScannedGoUrlOnAnyHostYieldsItsCode(string payload, string expected)
    {
        Assert.True(ShowcaseCodeParser.TryParseScan(payload, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("K7Q2X9")]
    [InlineData("https://example.com/menu")]
    [InlineData("https://example.com/api/v1/go/")]
    [InlineData("https://example.com/api/v1/go/K7Q2X9/extra")]
    [InlineData("https://example.com/api/v1/go/K7Q2X")]
    [InlineData("https://example.com/api/v1/go/K7Q2X0")]
    [InlineData("ftp://example.com/api/v1/go/K7Q2X9")]
    [InlineData("WIFI:S:home;T:WPA;P:secret;;")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseScannedIsNotAnAgendaMeCode(string? payload)
    {
        Assert.False(ShowcaseCodeParser.TryParseScan(payload, out var code));
        Assert.Equal(string.Empty, code);
    }

    [Theory]
    [InlineData("K7Q2X9", "K7Q2X9")]
    [InlineData("k7q2x9", "K7Q2X9")]
    [InlineData("  K7Q 2X9 ", "K7Q2X9")]
    [InlineData("k7q-2x9", "K7Q2X9")]
    [InlineData("K7Q–2X9", "K7Q2X9")]
    [InlineData("https://agendame.app/api/v1/go/K7Q2X9", "K7Q2X9")]
    public void ATypedCodeIsForgiving(string input, string expected)
    {
        Assert.True(ShowcaseCodeParser.TryParse(input, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("K7Q2X")]
    [InlineData("K7Q2X99")]
    [InlineData("K7Q2XO")]
    [InlineData("K7Q2X1")]
    [InlineData("K7Q2XI")]
    [InlineData("K7Q2XL")]
    [InlineData("K7Q2X0")]
    [InlineData("K7Q2X!")]
    [InlineData("   ")]
    public void ATypedCodeOutsideTheAlphabetOrLengthIsRejected(string input) =>
        Assert.False(ShowcaseCodeParser.TryParse(input, out _));

    [Fact]
    public void TheAlphabetHasNoLookAlikeCharacters()
    {
        Assert.Equal(31, ShowcaseCodeParser.Alphabet.Length);
        foreach (var c in "01ILO")
            Assert.DoesNotContain(c, ShowcaseCodeParser.Alphabet);
    }

    [Theory]
    [InlineData("K7Q2X9", "K7Q 2X9")]
    [InlineData("k7q-2x9", "K7Q 2X9")]
    [InlineData("ABC", "ABC")]
    public void DisplayGroupsTheCodeInThrees(string code, string expected) =>
        Assert.Equal(expected, ShowcaseCodeParser.FormatForDisplay(code));

    [Fact]
    public void TheDisplayedFormParsesBackToTheCode()
    {
        Assert.True(ShowcaseCodeParser.TryParse(ShowcaseCodeParser.FormatForDisplay("K7Q2X9"), out var code));
        Assert.Equal("K7Q2X9", code);
    }
}
