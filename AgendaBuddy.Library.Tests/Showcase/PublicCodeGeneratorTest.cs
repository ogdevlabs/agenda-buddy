using AgendaBuddy.Library.Showcase;
using Xunit;

namespace AgendaBuddy.Library.Tests.Showcase;

public class PublicCodeGeneratorTest
{
    [Fact]
    public void Next_IsSixCharactersFromTheUnambiguousAlphabet()
    {
        for (var i = 0; i < 500; i++)
        {
            var code = PublicCodeGenerator.Next();

            Assert.Equal(PublicCodeGenerator.Length, code.Length);
            Assert.All(code, c => Assert.Contains(c, PublicCodeGenerator.Alphabet));
        }
    }

    [Fact]
    public void Alphabet_ExcludesCharactersThatReadAlike()
    {
        foreach (var c in "01OIL")
            Assert.DoesNotContain(c, PublicCodeGenerator.Alphabet);
        Assert.Equal(PublicCodeGenerator.Alphabet.Length, PublicCodeGenerator.Alphabet.Distinct().Count());
    }

    [Fact]
    public void Next_IsNotRepetitive()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => PublicCodeGenerator.Next()).ToHashSet();

        Assert.True(codes.Count >= 995, $"only {codes.Count} distinct codes in 1000 draws");
    }

    [Fact]
    public void Next_RoundTripsThroughNormalise()
    {
        var code = PublicCodeGenerator.Next();

        Assert.Equal(code, PublicCodeGenerator.Normalise(code));
    }

    [Theory]
    [InlineData("abc234", "ABC234")]
    [InlineData("ABC-234", "ABC234")]
    [InlineData(" abc 234 ", "ABC234")]
    [InlineData("a-b-c-2-3-4", "ABC234")]
    public void Normalise_UppercasesAndStripsSpacesAndDashes(string input, string expected) =>
        Assert.Equal(expected, PublicCodeGenerator.Normalise(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC23")]
    [InlineData("ABC2345")]
    [InlineData("ABC10O")]
    [InlineData("ABCIL2")]
    [InlineData("ABC_23")]
    [InlineData("ÁBC234")]
    public void Normalise_AnythingNotAWellFormedCode_IsNull(string? input) =>
        Assert.Null(PublicCodeGenerator.Normalise(input));
}
