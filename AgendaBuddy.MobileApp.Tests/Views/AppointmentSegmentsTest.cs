using System.Xml.Linq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.Views;

public class AppointmentSegmentsTest
{
    private static readonly XDocument Page = LoadPage();

    private static XDocument LoadPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "agenda-buddy.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return XDocument.Load(Path.Combine(
            directory!.FullName, "AgendaBuddy.MobileApp", "Views", "CalendarPage.xaml"));
    }

    private static XElement Element(string automationId)
    {
        var element = Page.Descendants()
            .SingleOrDefault(node => (string?)node.Attribute("AutomationId") == automationId);

        Assert.NotNull(element);
        return element!;
    }

    [Fact]
    public void BothRolesUseTheSharedAppointmentsView()
    {
        Assert.NotNull(Element("AppointmentsView"));
        Assert.Equal("{Binding IsProvider}", (string?)Element("ManageCalendarButton").Attribute("IsVisible"));
    }

    [Theory]
    [InlineData("ScheduledAppointmentsTab", "Scheduled", "Appointments_Scheduled", "ScheduledCount")]
    [InlineData("DoneAppointmentsTab", "Done", "Appointments_Done", "DoneCount")]
    [InlineData("CancelledAppointmentsTab", "Cancelled", "Appointments_Cancelled", "CancelledCount")]
    public void EverySegmentHasLocalizedTextCountAndCommand(
        string automationId,
        string commandParameter,
        string resourceKey,
        string countProperty)
    {
        var segment = Element(automationId);
        var attributes = segment.DescendantsAndSelf().SelectMany(node => node.Attributes()).ToList();

        Assert.Contains(attributes, attribute =>
            attribute.Name.LocalName == "Command"
            && attribute.Value == "{Binding SelectAppointmentTabCommand}");
        Assert.Contains(attributes, attribute =>
            attribute.Name.LocalName == "CommandParameter"
            && attribute.Value == commandParameter);
        Assert.Contains(attributes, attribute =>
            attribute.Name.LocalName == "Text"
            && attribute.Value == $"{{x:Static strings:AppResources.{resourceKey}}}");
        Assert.Contains(attributes, attribute =>
            attribute.Name.LocalName == "Text"
            && attribute.Value == $"{{Binding {countProperty}}}");
    }

    [Fact]
    public void AppointmentRowsOpenTheExistingAppointmentDetailFlow()
    {
        var collection = Element("AppointmentsCollection");

        Assert.Contains(collection.Descendants().SelectMany(node => node.Attributes()), attribute =>
            attribute.Name.LocalName == "Command"
            && attribute.Value.Contains("OpenAppointmentFromListCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void HistoricalSegmentsExposeServerBackedPaginationControls()
    {
        var attributes = Page.Descendants().SelectMany(node => node.Attributes()).ToList();

        Assert.Contains(attributes, attribute => attribute.Value == "{Binding PreviousHistoryPageCommand}");
        Assert.Contains(attributes, attribute => attribute.Value == "{Binding NextHistoryPageCommand}");
        Assert.Contains(attributes, attribute => attribute.Value == "{Binding HistoryPageLabel}");
    }

    [Theory]
    [InlineData("CalendarPage.xaml", "{x:Static strings:AppResources.Xaml_Calendar}")]
    [InlineData("MessagingPage.xaml", "{x:Static strings:AppResources.Xaml_Messages}")]
    [InlineData("CustomersPage.xaml", "{Binding PageTitle}")]
    [InlineData("MorePage.xaml", "{x:Static strings:AppResources.Xaml_More}")]
    public void TabRootsUseTheSharedOperationalPageTitleStyle(string fileName, string titleResource)
    {
        var page = XDocument.Load(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Views", fileName));
        Assert.Equal("{StaticResource BackgroundPage}", (string?)page.Root!.Attribute("BackgroundColor"));

        var title = page.Descendants()
            .Single(element => element.Name.LocalName == "Label"
                && (string?)element.Attribute("Text") == titleResource);

        Assert.Equal("{StaticResource OperationalPageTitle}", (string?)title.Attribute("Style"));
        Assert.Contains(title.Ancestors(), ancestor =>
            (string?)ancestor.Attribute("Style") == "{StaticResource OperationalPageHeader}");
    }

    [Fact]
    public void OperationalPageStylesDefineOneTitleHierarchy()
    {
        var app = XDocument.Load(Path.Combine(RepoRoot(), "AgendaBuddy.MobileApp", "App.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
        var styles = app.Descendants("{http://schemas.microsoft.com/dotnet/2021/maui}Style")
            .ToDictionary(style => (string?)style.Attribute(x + "Key") ?? string.Empty);

        var header = styles["OperationalPageHeader"];
        Assert.Contains(header.Elements(), setter =>
            (string?)setter.Attribute("Property") == "BackgroundColor"
            && (string?)setter.Attribute("Value") == "{StaticResource Primary}");
        Assert.Contains(header.Elements(), setter =>
            (string?)setter.Attribute("Property") == "Padding"
            && (string?)setter.Attribute("Value") == "24,20");

        var title = styles["OperationalPageTitle"];
        Assert.Contains(title.Elements(), setter =>
            (string?)setter.Attribute("Property") == "FontSize"
            && (string?)setter.Attribute("Value") == "30");
        Assert.Contains(title.Elements(), setter =>
            (string?)setter.Attribute("Property") == "FontAttributes"
            && (string?)setter.Attribute("Value") == "Bold");
    }

    [Fact]
    public void DashboardUsesTheSharedOperationalPageTitleStyle()
    {
        var dashboard = XDocument.Load(Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Views", "DashboardPage.xaml"));
        var title = dashboard.Descendants()
            .Single(element => (string?)element.Attribute("AutomationId") == "DashboardOperationalTitle");

        Assert.Equal("{StaticResource OperationalPageTitle}", (string?)title.Attribute("Style"));
        Assert.Equal("{StaticResource OperationalPageHeader}", (string?)title.Parent!.Parent!.Attribute("Style"));
        Assert.Equal("{StaticResource BackgroundPage}", (string?)dashboard.Root!.Attribute("BackgroundColor"));

        var header = dashboard.Descendants()
            .Single(element => element.Name.LocalName == "BrandHeader");
        Assert.Equal("False", (string?)header.Attribute("ShowUser"));
        Assert.Equal("{Binding Greeting}", (string?)title.Attribute("Text"));
        Assert.DoesNotContain(title.Descendants(), element => element.Name.LocalName == "FormattedString");

        var name = dashboard.Descendants()
            .Single(element => (string?)element.Attribute("AutomationId") == "DashboardGreetingName");
        Assert.Equal("{Binding UserDisplayName}", (string?)name.Attribute("Text"));
        Assert.Equal("22", (string?)name.Attribute("FontSize"));
        Assert.Equal("TailTruncation", (string?)name.Attribute("LineBreakMode"));
        Assert.Equal("1", (string?)name.Attribute("MaxLines"));
    }

    [Fact]
    public void ContactsOperationalAccentsUseTheBrandPalette()
    {
        var contactsPath = Path.Combine(
            RepoRoot(), "AgendaBuddy.MobileApp", "Views", "CustomersPage.xaml");
        var contacts = File.ReadAllText(contactsPath);

        Assert.DoesNotContain("#AF52DE", contacts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#EDE9FE", contacts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#DBEAFE", contacts, StringComparison.OrdinalIgnoreCase);
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