using AgendaBuddy.MobileApp.Infrastructure;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// The brand header is app identity, so a new page shipping without it is a defect, not a style choice.
/// XAML is only compiled on the MAUI target frameworks, which this net10.0 test slice does not build, so
/// the views are inspected as text on disk.
/// </summary>
public class BrandHeaderPresenceTest
{
    private const string HeaderElement = "<controls:BrandHeader";

    /// <summary>Auth pages brand themselves ahead of any session existing, so they carry no shared header.</summary>
    private static readonly string[] PagesWithoutBrandHeader =
    [
        "ForgotPasswordPage.xaml",
        "LoginPage.xaml",
        "RegisterPage.xaml",
        "ResetPasswordConfirmPage.xaml",
    ];

    [Fact]
    public void EveryPageOutsideTheAuthFlowRendersTheBrandHeader()
    {
        var missing = ViewFiles()
            .Where(f => !PagesWithoutBrandHeader.Contains(Path.GetFileName(f)))
            .Where(f => !File.ReadAllText(f).Contains(HeaderElement, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(missing.Count == 0,
            "These views render no BrandHeader. Add one as the first element of the root layout, or, if the "
            + $"page is part of the auth flow, add it to {nameof(PagesWithoutBrandHeader)}: "
            + string.Join(", ", missing));
    }

    [Fact]
    public void TheAuthFlowExclusionListNamesOnlyPagesThatStillExist()
    {
        var actual = ViewFiles().Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);

        var stale = PagesWithoutBrandHeader.Where(p => !actual.Contains(p)).ToList();

        Assert.True(stale.Count == 0,
            $"{nameof(PagesWithoutBrandHeader)} names views that no longer exist, so the exclusion silently "
            + $"covers nothing: {string.Join(", ", stale)}");
    }

    [Fact]
    public void TheBrandHeaderIsTheFirstElementOfEachPagesRootLayout()
    {
        var wrong = new List<string>();

        foreach (var file in ViewFiles())
        {
            var lines = File.ReadAllLines(file);
            var header = Array.FindIndex(lines, l => l.Contains(HeaderElement, StringComparison.Ordinal));
            if (header < 0)
                continue;

            // Anything positioned in a grid row ahead of the header is rendering above app identity.
            var firstPositioned = Array.FindIndex(lines, l => l.Contains("Grid.Row=", StringComparison.Ordinal));
            if (firstPositioned != header)
                wrong.Add($"{Path.GetFileName(file)} (header at line {header + 1}, "
                          + $"something else positioned at line {firstPositioned + 1})");
        }

        Assert.True(wrong.Count == 0,
            "The BrandHeader must be the first positioned element of the root layout: " + string.Join(", ", wrong));
    }

    [Fact]
    public void TheBrandNameIsNotHardcodedInAnyView()
    {
        var offenders = ViewFiles()
            .Where(f => File.ReadAllText(f).Contains(AppBrand.Name, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"'{AppBrand.Name}' is spelled out in these views instead of coming from {nameof(AppBrand)}, so a "
            + $"rename would leave them behind: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void TheBrandNameIsCamelCased()
    {
        Assert.Equal("AgendaMe", AppBrand.Name);
    }

    private static IEnumerable<string> ViewFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views"), "*.xaml")
            .OrderBy(f => f, StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "agenda-buddy.sln")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
        }

        return current.FullName;
    }
}
