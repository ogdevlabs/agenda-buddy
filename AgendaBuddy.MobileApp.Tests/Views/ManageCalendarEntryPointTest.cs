using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// The provider's way into their working week is a labelled row, and it stays provider-only.
/// </summary>
/// <remarks>
/// <para>
/// It used to be a 40x40 button showing a bare <c>⚙</c> at 18pt, sitting between the two week-navigation
/// chevrons — so it read as a third arrow, and providers were not finding it. That matters more than the usual
/// discoverability argument: it is the only way to close a working day, and therefore the only way to stop being
/// booked at weekends.
/// </para>
/// <para>
/// XAML is not compiled on this <c>net10.0</c> slice, so the view is read as XML off disk.
/// </para>
/// </remarks>
public class ManageCalendarEntryPointTest
{
    private static XDocument CalendarPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(
            directory!.FullName, "AgendaBuddy.MobileApp", "Views", "CalendarPage.xaml"));
    }

    private static XElement TheRow()
    {
        var row = CalendarPage().Descendants()
            .FirstOrDefault(e => (string?)e.Attribute("AutomationId") == "ManageCalendarButton");

        Assert.NotNull(row);
        return row!;
    }

    /// <summary>A named affordance, not a glyph: the label is the whole point of the change.</summary>
    [Fact]
    public void TheEntryPointIsLabelled()
    {
        var text = TheRow().Descendants()
            .Select(e => (string?)e.Attribute("Text"))
            .Where(t => t is not null)
            .ToList();

        Assert.Contains("Manage calendar", text);
        // The subtitle says what is behind it, so "manage" is not left to mean anything.
        Assert.Contains(text, t => t!.Contains("Working days", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ⚠️ <b>Provider-only.</b> A customer has no working week to manage, and the row would lead to a screen that
    /// refuses them. The gate is the same <c>IsProvider</c> binding the old gear carried — this test is what keeps
    /// it attached now that the affordance is prominent rather than easily missed.
    /// </summary>
    [Fact]
    public void TheEntryPointIsProviderOnly()
    {
        Assert.Equal("{Binding IsProvider}", (string?)TheRow().Attribute("IsVisible"));
    }

    /// <summary>It has to actually go somewhere, and to the route the Shell registers.</summary>
    [Fact]
    public void TheEntryPointNavigatesToCalendarSettings()
    {
        Assert.Contains(TheRow().Descendants().Concat([TheRow()]),
            e => e.Attributes().Any(a => a.Name.LocalName == "Tapped"
                                         && a.Value == "OnCalendarSettingsTapped"));

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        var codeBehind = File.ReadAllText(Path.Combine(
            directory!.FullName, "AgendaBuddy.MobileApp", "Views", "CalendarPage.xaml.cs"));

        Assert.Contains("OnCalendarSettingsTapped", codeBehind, StringComparison.Ordinal);
        Assert.Contains("\"calendarSettings\"", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bare gear is gone, so there is exactly one way in — the same single-affordance rule the back button
    /// follows. Two entry points to one screen, one of them unlabelled, is what this replaced.
    /// </summary>
    [Fact]
    public void TheOldUnlabelledGearButtonIsGone()
    {
        var glyphOnlyButtons = CalendarPage().Descendants()
            .Where(e => e.Name.LocalName == "SafeButton")
            .Where(e => (string?)e.Attribute("Text") == "⚙")
            .ToList();

        Assert.Empty(glyphOnlyButtons);
    }
}
