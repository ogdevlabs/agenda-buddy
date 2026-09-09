using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

public class ProfessionsKeyboardDismissalTest
{
    [Fact]
    public void SearchKeyboardCanBeDismissedWithoutClearingTheQuery()
    {
        var mobileApp = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp");
        var page = XDocument.Load(Path.Combine(mobileApp, "Views", "ProfessionsPage.xaml"));
        var search = page.Descendants()
            .Single(element => element.Attribute("AutomationId")?.Value == "ProfessionSearchBar");

        Assert.Equal("True", page.Root?.Attribute("HideSoftInputOnTapped")?.Value);
        Assert.Equal("Search", search.Attribute("ReturnType")?.Value);
        Assert.Equal("OnProfessionSearchSubmitted", search.Attribute("SearchButtonPressed")?.Value);

        var codeBehind = File.ReadAllText(Path.Combine(mobileApp, "Views", "ProfessionsPage.xaml.cs"));
        Assert.Contains("OnProfessionSearchSubmitted", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ProfessionSearchBar.Unfocus()", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchText = string.Empty", codeBehind, StringComparison.Ordinal);
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

        throw new InvalidOperationException(
            $"Could not locate repo root (agenda-buddy.sln) walking up from {AppContext.BaseDirectory}.");
    }
}