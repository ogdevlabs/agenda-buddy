using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Calendar sync is for both roles, so both of its entry points must be visible to both: the Profile row and the
/// row on Appointments. A role binding on either would hide the feature from half the accounts with nothing failing.
/// </summary>
public class CalendarSyncEntryPointTest
{
    [Theory]
    [InlineData("ProfilePage", "CalendarSyncRow")]
    [InlineData("CalendarPage", "CalendarSyncEntryRow")]
    public void TheEntryPointIsShownToEveryRoleAndOpensCalendarSync(string view, string automationId)
    {
        var views = Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views");
        var row = XDocument.Load(Path.Combine(views, view + ".xaml")).Descendants()
            .SingleOrDefault(e => (string?)e.Attribute("AutomationId") == automationId);

        Assert.NotNull(row);
        for (var element = row; element is not null; element = element.Parent)
            Assert.Null(element.Attribute("IsVisible"));

        var handler = row!.Descendants().Select(e => (string?)e.Attribute("Tapped")).Single(h => h is not null);
        var codeBehind = File.ReadAllText(Path.Combine(views, view + ".xaml.cs"));
        var method = codeBehind.IndexOf($"void {handler}(", StringComparison.Ordinal);
        Assert.True(method >= 0, $"{view} has no {handler} handler.");
        Assert.Contains("\"calendarSync\"", codeBehind[method..(method + 200)], StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
