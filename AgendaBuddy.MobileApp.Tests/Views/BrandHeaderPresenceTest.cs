using System.Xml.Linq;
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

    /// <summary>
    /// The wordmark is drawn as two differently-coloured halves, so those halves have to still spell the
    /// name — a rename that touched only one of them would ship a misspelt brand.
    /// </summary>
    [Fact]
    public void TheTwoToneWordmarkHalvesSpellTheBrandName()
    {
        Assert.Equal(AppBrand.Name, AppBrand.NameStem + AppBrand.NameAccent);
        Assert.NotEmpty(AppBrand.NameStem);
        Assert.NotEmpty(AppBrand.NameAccent);
    }

    /// <summary>
    /// The name under the app icon is an MSBuild property, so it is the one place the brand cannot come from
    /// <see cref="AppBrand"/> and the one most likely to be left behind by a rename.
    /// </summary>
    [Fact]
    public void ThePackagedAppNameMatchesTheBrand()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "AgendaBuddy.MobileApp.csproj"));

        Assert.Contains($"<ApplicationTitle>{AppBrand.Name}</ApplicationTitle>", csproj);
    }

    /// <summary>
    /// Catches the previous name surviving anywhere a user can see it — a view, a code-behind, or the app
    /// manifest. Scoped to the mobile client; the solution, namespaces and repo keep the AgendaBuddy name.
    /// </summary>
    [Fact]
    public void ThePreviousBrandNameIsGoneFromTheClient()
    {
        var root = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp");

        var offenders = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".xaml", StringComparison.Ordinal)
                        || f.EndsWith(".cs", StringComparison.Ordinal)
                        || f.EndsWith(".csproj", StringComparison.Ordinal))
            // Build output restates every string in the project; only sources are the source of truth.
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin"))
            .Where(f => File.ReadAllText(f).Contains("Agenda Buddy", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"The app is called {AppBrand.Name}. These still say the old name: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// A sixth tab makes Shell generate its own overflow "More" list, which carries no brand header and no
    /// styling at all — and nothing else here can catch that, because the offending screen has no XAML of
    /// ours to inspect.
    /// </summary>
    [Fact]
    public void TheTabBarStaysAtFiveTabsSoShellGeneratesNoOverflowList()
    {
        var shell = XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "AppShell.xaml"));

        var tabs = shell.Descendants()
            .Where(e => e.Name.LocalName == "Tab")
            .Select(e => e.Attribute("Title")?.Value ?? "(untitled)")
            .ToList();

        Assert.True(tabs.Count <= 5,
            "iOS folds every tab past the fifth into an overflow list Shell builds itself, with none of the "
            + $"app's chrome. Move one into MorePage instead. Current tabs: {string.Join(", ", tabs)}");
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
