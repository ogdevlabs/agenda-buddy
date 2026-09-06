using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// A <c>RefreshView</c> whose <c>IsRefreshing</c> is bound to a general-purpose loading flag starts a refresh
/// nobody asked for, because every one of these pages fires its load command from <c>OnAppearing</c>.
/// </summary>
/// <remarks>
/// On iOS that begins a <c>UIRefreshControl</c> animation and inserts its content inset. When the loading flag
/// goes false before the control has finished animating in, the inset is left behind — a blank band above the
/// content that only a manual pull clears, because the pull resets the control. That is exactly what appeared
/// under the Dashboard's brand header.
/// <para>
/// Checked structurally rather than per page: the two flags look interchangeable, the symptom appears on one
/// platform only, and nothing about the binding reads as wrong.
/// </para>
/// </remarks>
public class RefreshViewBindingTest
{
    /// <summary>
    /// Property names that mean "something is loading" rather than "the user pulled to refresh". A
    /// <c>RefreshView</c> must not be driven by any of them.
    /// </summary>
    private static readonly string[] NotRefreshFlags = ["IsLoading", "IsBusy"];

    [Fact]
    public void NoRefreshViewIsDrivenByAGeneralPurposeLoadingFlag()
    {
        var offenders = new List<string>();

        foreach (var file in ViewFiles())
        {
            var root = XDocument.Load(file).Root;
            if (root is null)
                continue;

            foreach (var refresh in root.DescendantsAndSelf().Where(e => e.Name.LocalName == "RefreshView"))
            {
                var binding = refresh.Attribute("IsRefreshing")?.Value ?? string.Empty;

                foreach (var flag in NotRefreshFlags.Where(f =>
                             binding.Contains($"Binding {f}", StringComparison.Ordinal)))
                {
                    offenders.Add($"{Path.GetFileName(file)} (IsRefreshing bound to {flag})");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Give the view model a dedicated IsRefreshing property and a RefreshCommand that owns it, so a "
            + "programmatic load never drives the refresh control: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The other half: a <c>RefreshView</c> must actually be wired to something, or the gesture is inert and
    /// the spinner never clears.
    /// </summary>
    [Fact]
    public void EveryRefreshViewHasBothACommandAndAnIsRefreshingBinding()
    {
        var offenders = new List<string>();

        foreach (var file in ViewFiles())
        {
            var root = XDocument.Load(file).Root;
            if (root is null)
                continue;

            foreach (var refresh in root.DescendantsAndSelf().Where(e => e.Name.LocalName == "RefreshView"))
            {
                var missing = new List<string>();
                if (refresh.Attribute("Command") is null) missing.Add("Command");
                if (refresh.Attribute("IsRefreshing") is null) missing.Add("IsRefreshing");

                if (missing.Count > 0)
                    offenders.Add($"{Path.GetFileName(file)} (missing {string.Join(" and ", missing)})");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
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
