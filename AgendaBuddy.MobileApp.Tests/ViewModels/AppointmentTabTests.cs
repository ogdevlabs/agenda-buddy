using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// The appointment page's two sections: Manage and Notes.
/// </summary>
/// <remarks>
/// <para>
/// The session's own facts stay outside the tabs — they are the identity of the thing being looked at, not one of
/// its sections. What the tabs divide is what you can DO about it, which before this was a single column stacking
/// the facts, the customer's note, a payment row, the notes editor and a growing action row.
/// </para>
/// <para>
/// The rule worth reading here is that <b>Notes has no tab for a customer</b>. The backend note routes are
/// Provider-role-gated, so a tab a customer could select would open a section that can never hold anything.
/// </para>
/// </remarks>
public class AppointmentTabTests
{
    private const string Provider = "coach@example.com";
    private const string Customer = "me@example.com";

    private static Mock<IUserSessionService> Session(bool isProvider)
    {
        var session = new Mock<IUserSessionService>();
        session.SetupGet(s => s.Email).Returns(isProvider ? Provider : Customer);
        session.SetupGet(s => s.IsProvider).Returns(isProvider);
        session.SetupGet(s => s.IsCustomer).Returns(!isProvider);
        return session;
    }

    private static AppointmentDetailViewModel ViewModel(bool isProvider, bool withAppointment = true)
    {
        var vm = new AppointmentDetailViewModel(Mock.Of<IBookingApiService>(), Session(isProvider).Object);

        if (withAppointment)
        {
            vm.Appointment = new AppointmentDetail
            {
                Id = "abc123",
                ProviderEmail = Provider,
                CustomerEmail = Customer,
                ScheduledAt = DateTime.Now.AddDays(3),
                Status = AppointmentStatus.Booked,
                ServiceName = "1:1 Strength Session",
                ServiceDurationMinutes = 45
            };
        }

        return vm;
    }

    /// <summary>
    /// Manage is the default, because reschedule and cancel are why people open this page.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ManageIsTheTabTheyLandOn(bool isProvider)
    {
        var vm = ViewModel(isProvider);

        Assert.Equal(AppointmentTab.Manage, vm.SelectedTab);
        Assert.True(vm.IsManageTab);
        Assert.False(vm.IsNotesTab);
    }

    [Fact]
    public void ExactlyOneSectionIsVisibleAtATime()
    {
        var vm = ViewModel(isProvider: true);

        foreach (var tab in new[] { "Manage", "Notes" })
        {
            vm.SelectTabCommand.Execute(tab);

            var visible = new[] { vm.IsManageTab, vm.IsNotesTab }.Count(shown => shown);
            Assert.Equal(1, visible);
        }
    }

    /// <summary>
    /// A customer has no Notes tab at all — hidden, not disabled, because it could only ever open an empty
    /// section.
    /// </summary>
    [Fact]
    public void ACustomerHasNoNotesTab()
    {
        var customer = ViewModel(isProvider: false);
        var provider = ViewModel(isProvider: true);

        Assert.False(customer.ShowNotesTab);
        Assert.True(provider.ShowNotesTab);
    }

    /// <summary>
    /// And selecting it is refused rather than honoured, so nothing can route a customer into that section.
    /// </summary>
    [Fact]
    public void ACustomerCannotSelectNotesEvenIfAskedTo()
    {
        var vm = ViewModel(isProvider: false);

        vm.SelectTabCommand.Execute("Notes");

        Assert.Equal(AppointmentTab.Manage, vm.SelectedTab);
        Assert.False(vm.IsNotesTab);
    }

    /// <summary>
    /// Silently moving somebody off the tab they are on is more confusing than a tap that did nothing.
    /// </summary>
    /// <remarks>
    /// "Payment" is in the cases because it WAS a third tab: its name must now be ignored like any other
    /// unrecognised one, not quietly honoured by a leftover enum member.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nonsense")]
    [InlineData("Payment")]
    public void AnUnrecognisedTabNameIsIgnoredRatherThanResetting(string? name)
    {
        var vm = ViewModel(isProvider: true);
        vm.SelectTabCommand.Execute("Notes");

        vm.SelectTabCommand.Execute(name);

        Assert.Equal(AppointmentTab.Notes, vm.SelectedTab);
    }

    [Fact]
    public void TabNamesAreMatchedCaseInsensitively()
    {
        var vm = ViewModel(isProvider: true);

        vm.SelectTabCommand.Execute("notes");

        Assert.True(vm.IsNotesTab);
    }

    /// <summary>
    /// There are exactly two sections. A third would need a column in the strip, which is laid out for two.
    /// </summary>
    [Fact]
    public void ThereAreExactlyTwoTabs() =>
        Assert.Equal(2, Enum.GetValues<AppointmentTab>().Length);

    /// <summary>
    /// A customer has no Notes tab, so Manage spans both columns — a hidden segment still reserves its column,
    /// which would leave the strip visibly lopsided for every customer.
    /// </summary>
    [Fact]
    public void ManageSpansTheWholeStripWhenThereIsNoNotesTab()
    {
        Assert.Equal(2, ViewModel(isProvider: false).ManageTabColumnSpan);
        Assert.Equal(1, ViewModel(isProvider: true).ManageTabColumnSpan);
    }

    /// <summary>
    /// The Manage card holds actions that all read the appointment's status and role, so it must not render as an
    /// empty card of hidden buttons while a fetch is in flight.
    /// </summary>
    [Fact]
    public void ManageIsNotShownBeforeAnAppointmentLoads()
    {
        var vm = ViewModel(isProvider: true, withAppointment: false);

        Assert.True(vm.IsManageTab);
        Assert.False(vm.ShowManageSection);
    }

    [Fact]
    public void ManageIsShownOnceTheAppointmentIsThere()
    {
        var vm = ViewModel(isProvider: true);

        Assert.True(vm.ShowManageSection);

        vm.SelectTabCommand.Execute("Notes");
        Assert.False(vm.ShowManageSection);
    }


    /// <summary>
    /// A notes tab that renders nothing looks broken, so it says so — but not before the read has finished.
    /// </summary>
    [Fact]
    public void TheNotesEmptyStateIsNotClaimedWhileTheReadIsInFlight()
    {
        var vm = ViewModel(isProvider: true);

        vm.IsLoadingNotes = true;
        Assert.False(vm.HasNoSessionNotes);

        vm.IsLoadingNotes = false;
        Assert.True(vm.HasNoSessionNotes);
    }

    [Fact]
    public void TheNotesEmptyStateYieldsToAnError()
    {
        var vm = ViewModel(isProvider: true);
        Assert.True(vm.HasNoSessionNotes);

        vm.NotesErrorMessage = "Could not load notes.";

        // "No notes" and "could not read them" are different statements, and saying the first for the second
        // tells the provider their notes are gone.
        Assert.False(vm.HasNoSessionNotes);
    }

    [Fact]
    public void TheNotesEmptyStateGoesAwayOnceThereAreNotes()
    {
        var vm = ViewModel(isProvider: true);

        vm.Notes = [new NoteEntity { Content = "Worked on deadlifts." }];

        Assert.False(vm.HasNoSessionNotes);
        Assert.True(vm.HasSessionNotes);
    }
}
