using System.Text.RegularExpressions;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

public partial class XamlLocalizationGuardTest
{
    private static readonly HashSet<string> LocaleNeutralLiterals =
    [
        "  ·  ",
        "0.00",
        "555 123 4567",
        "Casey"
    ];

    [Fact]
    public void NoXamlFileIntroducesNewUserFacingLiterals()
    {
        var regressions = XamlFiles()
            .SelectMany(file => UserFacingLiteral().Matches(File.ReadAllText(file))
                .Select(match => new { File = Path.GetFileName(file), Value = match.Groups[1].Value }))
            .Where(item => !LocaleNeutralLiterals.Contains(item.Value))
            .Select(item => $"{item.File}: {item.Value}")
            .ToList();

        Assert.True(regressions.Count == 0,
            "New hardcoded user-facing XAML text was added. Add it to AppResources instead: "
            + string.Join(", ", regressions));
    }

    [Fact]
    public void EveryLocaleNeutralAllowlistEntryStillExists()
    {
        var actual = XamlFiles()
            .SelectMany(file => UserFacingLiteral().Matches(File.ReadAllText(file))
                .Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        var stale = LocaleNeutralLiterals.Where(value => !actual.Contains(value)).ToList();

        Assert.True(stale.Count == 0,
            "The XAML localization allowlist contains values no longer present: " + string.Join(", ", stale));
    }

    private static IEnumerable<string> XamlFiles()
    {
        var app = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp");
        return new[] { Path.Combine(app, "AppShell.xaml") }
            .Concat(Directory.EnumerateFiles(Path.Combine(app, "Views"), "*.xaml"))
            .Concat(Directory.EnumerateFiles(Path.Combine(app, "Controls"), "*.xaml"));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex("(?:Text|Title|Placeholder|SemanticProperties\\.Description|AutomationProperties\\.HelpText)=\"(?!\\{|&#|\\s*$)([^\"]+)\"")]
    private static partial Regex UserFacingLiteral();
}