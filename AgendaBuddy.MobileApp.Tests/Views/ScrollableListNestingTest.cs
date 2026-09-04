using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// A <c>CollectionView</c> inside a <c>ScrollView</c> is given unbounded height, so it renders its whole
/// content instead of scrolling, pushes everything below it off the page, and loses virtualization. That is
/// what buried ProfessionsPage's Save and Continue buttons under a ~120-item catalog. Checked structurally
/// rather than per page so it cannot come back somewhere else.
/// </summary>
public class ScrollableListNestingTest
{
    private static readonly string[] VirtualizingLists = ["CollectionView", "ListView", "CarouselView"];

    private const string Scroller = "ScrollView";

    /// <summary>
    /// Pre-existing nestings, kept passing so this guard can start blocking new ones. Each is a page whose
    /// nested list has not been measured against a long data set; none is asserted to be safe. Removing a
    /// name from here is the point — it should shrink, never grow.
    /// </summary>
    private static readonly string[] UnauditedNestings =
    [
        "AppointmentDetailPage.xaml",
        "BookAppointmentPage.xaml",
        "CalendarPage.xaml",
        "DashboardPage.xaml",
        "ServicesPage.xaml",
    ];

    [Fact]
    public void NoViewNestsAVirtualizingListInsideAScrollView()
    {
        var offenders = new List<string>();

        foreach (var file in ViewFiles().Where(f => !UnauditedNestings.Contains(Path.GetFileName(f))))
        {
            var root = XDocument.Load(file).Root;
            if (root is null)
                continue;

            foreach (var list in root.DescendantsAndSelf().Where(e => VirtualizingLists.Contains(e.Name.LocalName)))
            {
                if (list.Ancestors().Any(a => a.Name.LocalName == Scroller))
                    offenders.Add($"{Path.GetFileName(file)}: {list.Name.LocalName} inside a {Scroller}");
            }
        }

        Assert.True(offenders.Count == 0,
            $"Give the list its own bounded row in a Grid instead of nesting it in a {Scroller}: "
            + string.Join("; ", offenders));
    }

    [Fact]
    public void TheUnauditedListStillDescribesRealNestings()
    {
        var resolved = new List<string>();

        foreach (var name in UnauditedNestings)
        {
            var file = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", name);
            if (!File.Exists(file))
            {
                resolved.Add($"{name} (view no longer exists)");
                continue;
            }

            var root = XDocument.Load(file).Root;
            var stillNested = root is not null
                && root.DescendantsAndSelf()
                    .Where(e => VirtualizingLists.Contains(e.Name.LocalName))
                    .Any(e => e.Ancestors().Any(a => a.Name.LocalName == Scroller));

            if (!stillNested)
                resolved.Add($"{name} (nesting is gone)");
        }

        Assert.True(resolved.Count == 0,
            $"Remove these from {nameof(UnauditedNestings)} so the guard covers them again: "
            + string.Join(", ", resolved));
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
