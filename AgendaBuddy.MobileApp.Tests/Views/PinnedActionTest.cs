using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Some actions have to stay on screen no matter how much content is above them. A button placed inside a
/// <c>ScrollView</c> scrolls with its fields, which is how Add Service ended up below the fold — reachable
/// only after scrolling past five stacked inputs, and behind the keyboard while any of them was focused.
/// </summary>
public class PinnedActionTest
{
    /// <summary>
    /// The action's <c>AutomationId</c> and the view it lives on. Each must sit in a fixed row of a grid,
    /// outside every scroller on the page.
    /// </summary>
    public static TheoryData<string, string> PinnedActions => new()
    {
        { "AddServicePage.xaml", "AddServiceButton" },
        { "ServicesPage.xaml", "GoToAddServiceButton" },
        { "ProfessionsPage.xaml", "SaveProfessionsButton" },
        { "ProfessionsPage.xaml", "ContinueToServicesButton" },
        { "CalendarSettingsPage.xaml", "SaveCalendarHoursButton" },
    };

    [Theory]
    [MemberData(nameof(PinnedActions))]
    public void ThePinnedActionIsNotInsideAScroller(string view, string automationId)
    {
        var root = XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "Views", view)).Root;
        Assert.NotNull(root);

        var action = root!.Descendants()
            .SingleOrDefault(e => e.Attribute("AutomationId")?.Value == automationId);

        Assert.NotNull(action);
        Assert.DoesNotContain("ScrollView", action!.Ancestors().Select(a => a.Name.LocalName));
    }

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
