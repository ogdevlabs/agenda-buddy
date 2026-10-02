using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

/// <summary>
/// Scanning a provider's code is how a customer subscribes to that provider, so the entry point is customer-only.
/// A provider cannot subscribe to another provider, and a row leading there would offer them something they
/// cannot do.
/// </summary>
/// <remarks>XAML is not compiled on this <c>net10.0</c> slice, so the view is read as XML off disk.</remarks>
public class ScanCodeEntryPointTest
{
    private static string ViewsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "AgendaBuddy.MobileApp", "Views");
    }

    [Fact]
    public void MoreShowsTheScanRowToCustomersOnly()
    {
        var views = ViewsDirectory();
        var more = XDocument.Load(Path.Combine(views, "MorePage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";

        var row = more.Descendants().Single(e => (string?)e.Attribute("AutomationId") == "MoreScanCodeRow");
        Assert.Equal("ScanCodeRow", (string?)row.Attribute(x + "Name"));
        // Hidden until the role is known, so a provider never sees it flash in.
        Assert.Equal("False", (string?)row.Attribute("IsVisible"));

        var divider = more.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "ScanCodeDivider");
        Assert.Equal("False", (string?)divider.Attribute("IsVisible"));

        var codeBehind = File.ReadAllText(Path.Combine(views, "MorePage.xaml.cs"));
        Assert.Contains("ScanCodeRow.IsVisible = _session.IsCustomer;", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ScanCodeDivider.IsVisible = _session.IsCustomer;", codeBehind, StringComparison.Ordinal);
    }
}
