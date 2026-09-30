using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Copy about a provider or a customer never assumes a gender. "See her work" is wrong for most of the people it is
/// shown about, so user-facing text says "their", uses the name, or rephrases.
/// </summary>
public class GenderNeutralCopyTest
{
    private static readonly Regex Pronoun = new(@"\b(her|his|she|he)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex StringLiteral = new(@"(?<!')""(?:[^""\\\r\n]|\\.)*""", RegexOptions.CultureInvariant);

    [Fact]
    public void NoXamlAttributeValueUsesAGenderedPronoun()
    {
        var hits = XamlFiles()
            .SelectMany(file => XDocument.Load(file).Root!.DescendantsAndSelf()
                .SelectMany(element => element.Attributes())
                .Where(attribute => !attribute.IsNamespaceDeclaration && Pronoun.IsMatch(attribute.Value))
                .Select(attribute => $"{Relative(file)}: {attribute.Name.LocalName}=\"{attribute.Value}\""))
            .ToList();

        Assert.True(hits.Count == 0, "Gendered pronoun in XAML:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void NoViewModelOrInfrastructureStringLiteralUsesAGenderedPronoun()
    {
        var hits = CSharpFiles("ViewModels").Concat(CSharpFiles("Infrastructure"))
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (line, number: index + 1))
                .Where(entry => !entry.line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .SelectMany(entry => StringLiteral.Matches(entry.line)
                    .Where(match => Pronoun.IsMatch(match.Value))
                    .Select(match => $"{Relative(file)}:{entry.number}: {match.Value}")))
            .ToList();

        Assert.True(hits.Count == 0, "Gendered pronoun in a string literal:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void NoEnglishResourceValueUsesAGenderedPronoun()
    {
        var resx = Path.Combine(AppRoot(), "Resources", "Strings", "AppResources.resx");

        var hits = XDocument.Load(resx).Root!.Elements("data")
            .Select(data => (name: data.Attribute("name")?.Value, value: data.Element("value")?.Value ?? string.Empty))
            .Where(entry => Pronoun.IsMatch(entry.value))
            .Select(entry => $"{entry.name}: {entry.value}")
            .ToList();

        Assert.True(hits.Count == 0, "Gendered pronoun in AppResources.resx:\n" + string.Join("\n", hits));
    }

    [Theory]
    [InlineData("See her work", true)]
    [InlineData("HIS portfolio", true)]
    [InlineData("She is available", true)]
    [InlineData("he", true)]
    [InlineData("See their work", false)]
    [InlineData("Share the showcase", false)]
    [InlineData("Hello there", false)]
    [InlineData("the header", false)]
    public void ThePatternMatchesWholeWordsOnly(string text, bool expected) =>
        Assert.Equal(expected, Pronoun.IsMatch(text));

    private static IEnumerable<string> XamlFiles()
    {
        var root = AppRoot();
        return Walk(Path.Combine(root, "Views"), "*.xaml")
            .Concat(Walk(Path.Combine(root, "Controls"), "*.xaml"))
            .Append(Path.Combine(root, "AppShell.xaml"))
            .Where(File.Exists);
    }

    private static IEnumerable<string> CSharpFiles(string folder) => Walk(Path.Combine(AppRoot(), folder), "*.cs");

    private static IEnumerable<string> Walk(string directory, string pattern)
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var file in Directory.EnumerateFiles(directory, pattern))
            yield return file;

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);
            if (name.StartsWith('.') || name is "bin" or "obj")
                continue;

            foreach (var file in Walk(child, pattern))
                yield return file;
        }
    }

    private static string Relative(string path) => Path.GetRelativePath(AppRoot(), path);

    private static string AppRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
            current = current.Parent;

        if (current is null)
            throw new InvalidOperationException($"Could not locate repo root walking up from {AppContext.BaseDirectory}.");

        return Path.Combine(current.FullName, "AgendaBuddy.MobileApp");
    }
}
