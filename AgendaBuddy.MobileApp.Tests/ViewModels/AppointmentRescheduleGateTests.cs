using AgendaBuddy.Library.Entities;
using AgendaBuddy.MobileApp.Models;
using AgendaBuddy.MobileApp.Services;
using AgendaBuddy.MobileApp.ViewModels;
using Moq;
using Xunit;

namespace AgendaBuddy.MobileApp.Tests.ViewModels;

/// <summary>
/// Which appointment actions are OFFERED, by status and by role.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every gate here hides rather than disables, and that is the point being tested.</b> A button the server will
/// refuse is the defect class that made a messaging <c>403</c> surface to the user as a connection failure — the
/// affordance was ungated, the server said no, and the client blamed the network. These assertions are what stop
/// the same shape reappearing on four new actions.
/// </para>
/// <para>
/// The gates read the appointment already on screen, so they cost no request and are exactly as fresh as the card
/// they sit on. The server remains authoritative — the device clock is not trustworthy — so this is courtesy, not
/// enforcement.
/// </para>
/// </remarks>
public class AppointmentRescheduleGateTests
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

    private static AppointmentDetailViewModel ViewModel(
        bool isProvider,
        AppointmentStatus status = AppointmentStatus.Booked,
        double hoursAhead = 72,
        DateTime? proposedStart = null,
        string proposedBy = "",
        DateTime? previousStart = null)
    {
        var vm = new AppointmentDetailViewModel(Mock.Of<IBookingApiService>(), Session(isProvider).Object)
        {
            Appointment = new AppointmentDetail
            {
                Id = "abc123",
                ProviderEmail = Provider,
                CustomerEmail = Customer,
                ScheduledAt = DateTime.Now.AddHours(hoursAhead),
                Status = status,
                ServiceName = "1:1 Strength Session",
                ServiceDurationMinutes = 45,
                ProposedStart = proposedStart,
                ProposedBy = proposedBy,
                PreviousStart = previousStart
            }
        };

        return vm;
    }

    // ── Who may move a session ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The calendar is the PROVIDER'S, so they move sessions on it and a customer asks. Exactly one of the two
    /// buttons is ever visible — showing both would offer a customer a route that answers 403.
    /// </summary>
    [Fact]
    public void OnlyTheProviderIsOfferedAnOutrightReschedule()
    {
        var provider = ViewModel(isProvider: true);
        var customer = ViewModel(isProvider: false);

        Assert.True(provider.ShowRescheduleButton);
        Assert.False(provider.ShowRequestNewTimeButton);

        Assert.False(customer.ShowRescheduleButton);
        Assert.True(customer.ShowRequestNewTimeButton);
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.RescheduleRequested)]
    public void NeitherSideIsOfferedARescheduleUnlessTheSessionIsBooked(AppointmentStatus status)
    {
        Assert.False(ViewModel(isProvider: true, status: status).ShowRescheduleButton);
        Assert.False(ViewModel(isProvider: false, status: status).ShowRequestNewTimeButton);
    }

    /// <summary>
    /// Confirming is the provider accepting a REQUEST. Offering it on an already-booked session is offering to
    /// re-do a transition the entity refuses.
    /// </summary>
    [Fact]
    public void ConfirmIsOfferedOnlyToAProviderOnARequestedSession()
    {
        Assert.True(ViewModel(isProvider: true, status: AppointmentStatus.Requested).ShowConfirmButton);
        Assert.False(ViewModel(isProvider: true, status: AppointmentStatus.Booked).ShowConfirmButton);
        Assert.False(ViewModel(isProvider: false, status: AppointmentStatus.Requested).ShowConfirmButton);
    }

    // ── Answering a proposal ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The server refuses an answer from whoever made the proposal — otherwise asking and agreeing would be one
    /// act, and a customer could move a provider's session unilaterally. So the proposer is shown a waiting
    /// message, and the other party gets the buttons.
    /// </summary>
    [Fact]
    public void OnlyThePartyWhoDidNotProposeIsOfferedApproveAndDecline()
    {
        var proposed = DateTime.Now.AddDays(4);

        var proposer = ViewModel(
            isProvider: false, status: AppointmentStatus.RescheduleRequested,
            proposedStart: proposed, proposedBy: Customer);

        var answerer = ViewModel(
            isProvider: true, status: AppointmentStatus.RescheduleRequested,
            proposedStart: proposed, proposedBy: Customer);

        Assert.True(proposer.IsMyPendingReschedule);
        Assert.False(proposer.ShowRescheduleAnswerButtons);

        Assert.False(answerer.IsMyPendingReschedule);
        Assert.True(answerer.ShowRescheduleAnswerButtons);
    }

    [Fact]
    public void ProposerMatchingIsCaseInsensitive()
    {
        var vm = ViewModel(
            isProvider: false, status: AppointmentStatus.RescheduleRequested,
            proposedStart: DateTime.Now.AddDays(4), proposedBy: "ME@EXAMPLE.COM");

        Assert.True(vm.IsMyPendingReschedule);
    }

    /// <summary>
    /// A status of <c>RescheduleRequested</c> with no proposed time is not a pending proposal. Treating it as one
    /// would draw a banner naming no time and offer two buttons that both refuse.
    /// </summary>
    [Fact]
    public void TheStatusAloneIsNotAPendingProposal()
    {
        var vm = ViewModel(isProvider: true, status: AppointmentStatus.RescheduleRequested);

        Assert.False(vm.HasPendingReschedule);
        Assert.False(vm.ShowRescheduleAnswerButtons);
    }

    /// <summary>
    /// The pending banner is worded entirely differently by who is waiting on whom, and BOTH wordings restate
    /// where the session still is — a proposed time is not the appointment.
    /// </summary>
    [Fact]
    public void ThePendingMessageNamesTheProposedTimeAndWhereTheSessionStillIs()
    {
        var proposed = DateTime.Now.AddDays(4);

        var proposer = ViewModel(
            isProvider: false, status: AppointmentStatus.RescheduleRequested,
            proposedStart: proposed, proposedBy: Customer);
        var answerer = ViewModel(
            isProvider: true, status: AppointmentStatus.RescheduleRequested,
            proposedStart: proposed, proposedBy: Customer);

        Assert.Contains("You asked", proposer.PendingRescheduleMessage);
        Assert.Contains("was requested", answerer.PendingRescheduleMessage);

        // The invariant is that BOTH name the proposed time AND where the session currently still is — not any
        // particular phrasing. A banner that names only the proposal reads as though the session had moved.
        foreach (var (message, appointment) in new[]
                 {
                     (proposer.PendingRescheduleMessage, proposer.Appointment!),
                     (answerer.PendingRescheduleMessage, answerer.Appointment!)
                 })
        {
            Assert.Contains(proposed.ToString("dddd d MMMM"), message);
            Assert.Contains(appointment.ScheduledAt.ToString("dddd d MMMM"), message);
        }
    }

    /// <summary>
    /// A completed reschedule has to be visible AS one, or it reads exactly like a session that was always at
    /// this time and somebody who half-remembers the old slot cannot confirm the change was real.
    /// </summary>
    [Fact]
    public void ACompletedRescheduleShowsWhereTheSessionWas()
    {
        var previous = DateTime.Now.AddDays(-1);
        var vm = ViewModel(isProvider: false, previousStart: previous);

        Assert.True(vm.WasRescheduled);
        Assert.Contains("Moved from", vm.PreviousTimeLabel);
        Assert.Contains(previous.ToString("ddd d MMM"), vm.PreviousTimeLabel);
    }

    [Fact]
    public void AnAppointmentNeverRescheduledShowsNoPreviousTime()
    {
        var vm = ViewModel(isProvider: false);

        Assert.False(vm.WasRescheduled);
        Assert.Empty(vm.PreviousTimeLabel);
    }

    // ── Cancellation and its notice period ────────────────────────────────────────────────────────────

    /// <summary>
    /// A provider may cancel at any notice — a session they cannot make is unavoidable. A customer cancelling an
    /// hour beforehand costs the provider a slot nobody else can take, which is the asymmetry.
    /// </summary>
    [Theory]
    [InlineData(72)]
    [InlineData(2)]
    [InlineData(0.5)]
    public void AProviderIsAlwaysOfferedCancel(double hoursAhead)
    {
        var vm = ViewModel(isProvider: true, hoursAhead: hoursAhead);

        Assert.True(vm.ShowCancelButton);
        Assert.False(vm.ShowCancellationClosedNotice);
    }

    [Theory]
    [InlineData(72, true)]
    [InlineData(25, true)]
    [InlineData(23, false)]
    [InlineData(1, false)]
    public void ACustomerIsOfferedCancelOnlyOutsideTheNoticePeriod(double hoursAhead, bool offered)
    {
        var vm = ViewModel(isProvider: false, hoursAhead: hoursAhead);

        Assert.Equal(offered, vm.ShowCancelButton);

        // And the two are mutually exclusive: the reason takes the button's place rather than appearing beside it.
        Assert.Equal(!offered, vm.ShowCancellationClosedNotice);
    }

    /// <summary>
    /// A missing button with no explanation is indistinguishable from a bug, so the closed notice names the
    /// deadline as a concrete local time — "too late" leaves the reader unable to tell how late.
    /// </summary>
    [Fact]
    public void TheClosedNoticeNamesTheDeadline()
    {
        var vm = ViewModel(isProvider: false, hoursAhead: 3);

        Assert.Contains("Cancellations closed", vm.CancellationClosedMessage);
        Assert.Contains(
            vm.Appointment!.CustomerCancellationDeadline.ToString("ddd d MMM"), vm.CancellationClosedMessage);
    }

    /// <summary>
    /// Shown while cancelling is still possible, so the deadline is not discovered by being refused. Only to a
    /// customer — a provider has no deadline to be told about.
    /// </summary>
    [Fact]
    public void TheDeadlineIsShownToACustomerWhileStillCancellable()
    {
        var customer = ViewModel(isProvider: false, hoursAhead: 72);
        var provider = ViewModel(isProvider: true, hoursAhead: 72);

        Assert.True(customer.ShowCancellationDeadlineNotice);
        Assert.Contains("Free to cancel until", customer.CancellationDeadlineMessage);

        Assert.False(provider.ShowCancellationDeadlineNotice);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void NeitherRoleIsOfferedCancelOnAFinishedSession(AppointmentStatus status)
    {
        foreach (var isProvider in new[] { true, false })
        {
            var vm = ViewModel(isProvider, status: status);

            Assert.False(vm.ShowCancelButton);

            // Nor the explanation: there is no deadline story to tell about a session that is already over.
            Assert.False(vm.ShowCancellationClosedNotice);
        }
    }

    /// <summary>
    /// A pending proposal must not trap the appointment. Cancelling stays available to both sides, mirroring the
    /// server, where <c>RescheduleRequested</c> is in the cancellable set.
    /// </summary>
    [Fact]
    public void CancelStaysAvailableWhileAProposalIsOutstanding()
    {
        var vm = ViewModel(
            isProvider: true, status: AppointmentStatus.RescheduleRequested,
            proposedStart: DateTime.Now.AddDays(4), proposedBy: Customer);

        Assert.True(vm.ShowCancelButton);
    }

    /// <summary>
    /// Nothing is offered at all before the appointment loads — otherwise the row briefly shows actions for an
    /// appointment nobody is looking at yet.
    /// </summary>
    [Fact]
    public void NoActionIsOfferedWithoutAnAppointment()
    {
        var vm = new AppointmentDetailViewModel(
            Mock.Of<IBookingApiService>(), Session(isProvider: true).Object);

        Assert.False(vm.ShowRescheduleButton);
        Assert.False(vm.ShowRequestNewTimeButton);
        Assert.False(vm.ShowCancelButton);
        Assert.False(vm.ShowCancellationClosedNotice);
        Assert.False(vm.HasPendingReschedule);
        Assert.False(vm.ShowRescheduleAnswerButtons);
    }
}
