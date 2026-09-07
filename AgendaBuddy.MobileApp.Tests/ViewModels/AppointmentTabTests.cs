using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// The appointment page's three sections: Manage, Payment, Notes.
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
        Assert.False(vm.IsPaymentTab);
        Assert.False(vm.IsNotesTab);
    }

    [Fact]
    public void ExactlyOneSectionIsVisibleAtATime()
    {
        var vm = ViewModel(isProvider: true);

        foreach (var tab in new[] { "Manage", "Payment", "Notes" })
        {
            vm.SelectTabCommand.Execute(tab);

            var visible = new[] { vm.IsManageTab, vm.IsPaymentTab, vm.IsNotesTab }.Count(shown => shown);
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
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nonsense")]
    public void AnUnrecognisedTabNameIsIgnoredRatherThanResetting(string? name)
    {
        var vm = ViewModel(isProvider: true);
        vm.SelectTabCommand.Execute("Payment");

        vm.SelectTabCommand.Execute(name);

        Assert.Equal(AppointmentTab.Payment, vm.SelectedTab);
    }

    [Fact]
    public void TabNamesAreMatchedCaseInsensitively()
    {
        var vm = ViewModel(isProvider: true);

        vm.SelectTabCommand.Execute("payment");

        Assert.True(vm.IsPaymentTab);
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

        vm.SelectTabCommand.Execute("Payment");
        Assert.False(vm.ShowManageSection);
    }

    /// <summary>
    /// A payment section that shows no amount leaves the reader to remember what the session costs.
    /// </summary>
    [Fact]
    public void ThePaymentSummaryNamesTheServiceItsLengthAndWhen()
    {
        var vm = ViewModel(isProvider: false);

        Assert.Contains("1:1 Strength Session", vm.PaymentSummary);
        Assert.Contains("45 min", vm.PaymentSummary);
        Assert.Contains(vm.Appointment!.ScheduledAt.ToString("ddd d MMM"), vm.PaymentSummary);
    }

    /// <summary>
    /// A session booked before services were selectable has no recorded length, so the summary omits it rather
    /// than inventing one.
    /// </summary>
    [Fact]
    public void ThePaymentSummaryOmitsALengthItDoesNotHave()
    {
        var vm = ViewModel(isProvider: false);
        vm.Appointment = new AppointmentDetail
        {
            Id = "abc123",
            ScheduledAt = DateTime.Now.AddDays(3),
            ServiceName = "1:1 Strength Session"
        };

        Assert.DoesNotContain("min", vm.PaymentSummary);
        Assert.Contains("1:1 Strength Session", vm.PaymentSummary);
    }

    [Fact]
    public void ThePaymentSummaryIsEmptyWithoutAnAppointment()
    {
        var vm = ViewModel(isProvider: false, withAppointment: false);

        Assert.Empty(vm.PaymentSummary);
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
