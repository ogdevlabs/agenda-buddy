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
}