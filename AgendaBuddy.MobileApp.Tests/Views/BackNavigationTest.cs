using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Back navigation lives in <c>BrandHeader</c>, once, and no view declares its own.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Every view in this app hides the native navigation bar</b> (<c>Shell.NavBarIsVisible="False"</c>),
/// which hides the platform's back button with it. So a pushed page with no affordance of its own can only be
/// left by the iOS edge-swipe gesture, with nothing on screen saying so — and that is what happened: nine pages
/// had grown their own "← Back" button and every other pushed page had nothing at all. The Language screen was
/// the one a user actually got stuck on.
/// </para>
/// <para>
/// The fix is one back button in the header, shown from the Shell navigation depth, so a new view gets one by
/// existing. These tests hold both halves: the header still has it, and no page has re-grown one beside it.
/// XAML is not compiled on this <c>net10.0</c> slice, so the views are read as text off disk.
/// </para>
/// </remarks>
public class BackNavigationTest
{
    /// <summary>The one back affordance, in the one element every non-auth view carries.</summary>
    [Fact]
    public void TheBrandHeaderCarriesABackButton()
    {
        var header = File.ReadAllText(Path.Combine(MobileAppRoot(), "Controls", "BrandHeader.xaml"));

        Assert.Contains("BrandHeaderBackButton", header, StringComparison.Ordinal);
        Assert.Contains("OnBackTapped", header, StringComparison.Ordinal);
    }

    /// <summary>
    /// Its visibility is derived, not hardcoded — a header that always showed the button would offer one on
    /// every tab root, where it has nothing to pop.
    /// </summary>
    [Fact]
    public void TheBackButtonsVisibilityComesFromTheNavigationDepth()
    {
        var header = File.ReadAllText(Path.Combine(MobileAppRoot(), "Controls", "BrandHeader.xaml"));
        var code = File.ReadAllText(Path.Combine(MobileAppRoot(), "Controls", "BrandHeader.xaml.cs"));

        Assert.Contains("IsVisible=\"{Binding CanGoBack", header, StringComparison.Ordinal);
        Assert.Contains("NavigationStack", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>No page declares its own back button.</b> A second one beside the header's is the same
    /// double-chrome defect as the double header (<c>agenda-buddy-po0</c>), and per-page affordances are exactly
    /// what drifted into nine pages having one and the rest having none.
    /// </summary>
    [Fact]
    public void NoViewDeclaresItsOwnBackButton()
    {
        var offenders = ViewFiles()
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("&#x2190; Back", StringComparison.Ordinal)
                       || text.Contains("OnBackClicked", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These views declare their own back button. BrandHeader already provides one on every pushed page, "
            + "so this is a second affordance beside it: "
            + string.Join(", ", offenders));
    }

    /// <summary>No orphaned handler either — a dead one is the next person's reason to re-add the button.</summary>
    [Fact]
    public void NoCodeBehindKeepsAnOrphanedBackHandler()
    {
        var offenders = Directory
            .EnumerateFiles(Path.Combine(MobileAppRoot(), "Views"), "*.xaml.cs")
            .Where(file => File.ReadAllText(file).Contains("OnBackClicked", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"These code-behinds keep an unreferenced OnBackClicked handler: {string.Join(", ", offenders)}");
    }

    private static IEnumerable<string> ViewFiles() =>
        Directory.EnumerateFiles(Path.Combine(MobileAppRoot(), "Views"), "*.xaml");

    /// <summary>
    /// Walks up to the repository root and back down into the app project.
    /// </summary>
    /// <remarks>
    /// Segments beginning with a dot are skipped by every tree-walking test here, so a parallel git worktree
    /// building at the same time cannot be mistaken for this one's sources.
    /// </remarks>
    private static string MobileAppRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "AgendaBuddy.MobileApp");
    }
}
