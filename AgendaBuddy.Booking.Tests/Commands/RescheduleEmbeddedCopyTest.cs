using System;
using System.Threading;
using System.Threading.Tasks;
using AgendaBuddy.Booking.Core.Commands;
using AgendaBuddy.Booking.Domain.Commands;
using AgendaBuddy.EventAndCommands.Persistence;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Services;
using MediatR;
using Moq;
using Xunit;

namespace AgendaBuddy.Booking.Tests.Commands;

/// <summary>
/// Every reschedule must reach the provider's EMBEDDED appointment copy, not only the <c>appointments</c>
/// collection.
/// </summary>
/// <remarks>
/// <para>
/// <b>The embedded list is the client-facing read.</b> <c>GET /api/v1/calendar/appointments/{email}</c> serves
/// <c>ProviderEntity.AppointmentEntities</c> — for a customer too, since <c>CustomerEntity</c> holds only
/// identifier strings — and it is also what <c>AvailabilityCalculator</c> reads for the busy set. So a write that
/// updates only the collection has two silent consequences, both found live: a proposal reaches no screen (the
/// status arrives as <c>RescheduleRequested</c> with no proposed time, which every reader correctly treats as no
/// proposal, so the banner never draws and Approve/Decline never appear), and a completed move leaves the OLD slot
/// blocked while offering the new one, which is a double-booking generator.
/// </para>
/// <para>
/// These assertions are on the CALLS rather than on stored state, because the defect was never a wrong value — it
/// was a write that did not happen at all.
/// </para>
/// </remarks>
public class RescheduleEmbeddedCopyTest
{
    private const string ProviderEmail = "coach@example.com";
    private const string CustomerEmail = "ada@example.com";

    private static readonly DateTime Start = DateTime.UtcNow.AddDays(5);
    private static readonly DateTime Proposed = DateTime.UtcNow.AddDays(6);

    private static AppointmentEntity Appointment(
        AppointmentStatus status = AppointmentStatus.Booked,
        DateTime? proposedStart = null,
        string? proposedBy = null) =>
        new()
        {
            Identifier = "abc123",
            EmailProvider = ProviderEmail,
            EmailCustomer = CustomerEmail,
            Start = Start,
            End = Start.AddMinutes(45),
            AppointmentStatus = status,
            ServiceDurationMinutes = 45,
            ProposedStart = proposedStart,
            ProposedBy = proposedBy
        };

    private static Mock<IBookingService> Bookings(AppointmentEntity appointment)
    {
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.SearchAppointmentAsync("abc123")).ReturnsAsync(appointment);
        bookings.Setup(b => b.ApplyRescheduleAsync(
                It.IsAny<string>(), It.IsAny<AppointmentEntity>(), It.IsAny<AppointmentStatus>()))
            .ReturnsAsync(true);
        bookings.Setup(b => b.RecordRescheduleProposalAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        bookings.Setup(b => b.ClearRescheduleProposalAsync(It.IsAny<string>())).ReturnsAsync(true);
        return bookings;
    }

    /// <summary>
    /// A customer's proposal has to land on the embedded copy WITH its proposed time and proposer — not merely as
    /// a status change, which is what made a pending request invisible to every screen.
    /// </summary>
    [Fact]
    public async Task RequestingAReschedule_WritesTheProposalOntoTheEmbeddedCopy()
    {
        var appointment = Appointment();
        var providers = new Mock<IProviderService>();

        var handler = new RequestRescheduleCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Bookings(appointment).Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(
            new RequestRescheduleCommand
            {
                Identifier = "abc123",
                ProposedStartUtc = Proposed,
                RequestedByEmail = CustomerEmail
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        providers.Verify(
            p => p.SetEmbeddedRescheduleProposalAsync(ProviderEmail, "abc123", Proposed, CustomerEmail),
            Times.Once);

        // Specifically NOT the status-only write: it is what the defect looked like.
        providers.Verify(
            p => p.ChangeEmbeddedAppointmentStatusAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AppointmentStatus>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// Approving must move the embedded copy AND record where it was, or the freed slot stays blocked and the
    /// "moved from" line has nothing to render.
    /// </summary>
    [Fact]
    public async Task ApprovingAReschedule_MovesTheEmbeddedCopyAndRecordsThePreviousStart()
    {
        var appointment = Appointment(
            AppointmentStatus.RescheduleRequested, proposedStart: Proposed, proposedBy: CustomerEmail);
        var providers = new Mock<IProviderService>();

        var handler = new AnswerRescheduleCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Bookings(appointment).Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(
            new AnswerRescheduleCommand
            {
                Identifier = "abc123",
                Approve = true,
                AnsweredByEmail = ProviderEmail
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        providers.Verify(
            p => p.ChangeEmbeddedAppointmentScheduleAsync(
                ProviderEmail,
                "abc123",
                Proposed,
                It.Is<DateTime>(end => end == Proposed.AddMinutes(45)),
                It.Is<DateTime?>(previous => previous == Start)),
            Times.Once);
    }

    /// <summary>
    /// Declining must CLEAR the proposal from the embedded copy, not only set the status back. A <c>Booked</c>
    /// appointment still carrying a proposed time reads as an outstanding request against a status saying there
    /// is none.
    /// </summary>
    [Fact]
    public async Task DecliningAReschedule_ClearsTheProposalFromTheEmbeddedCopy()
    {
        var appointment = Appointment(
            AppointmentStatus.RescheduleRequested, proposedStart: Proposed, proposedBy: CustomerEmail);
        var providers = new Mock<IProviderService>();

        var handler = new AnswerRescheduleCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Bookings(appointment).Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(
            new AnswerRescheduleCommand
            {
                Identifier = "abc123",
                Approve = false,
                AnsweredByEmail = ProviderEmail
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        providers.Verify(
            p => p.ClearEmbeddedRescheduleProposalAsync(ProviderEmail, "abc123"), Times.Once);

        // The times must not move on a decline.
        providers.Verify(
            p => p.ChangeEmbeddedAppointmentScheduleAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<DateTime?>()),
            Times.Never);
    }

    /// <summary>
    /// A provider's outright move gets the same treatment as an approval — same write, same previous_start.
    /// </summary>
    [Fact]
    public async Task AProviderMove_MovesTheEmbeddedCopyAndRecordsThePreviousStart()
    {
        var appointment = Appointment();
        var providers = new Mock<IProviderService>();

        var handler = new RescheduleAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Bookings(appointment).Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(
            new RescheduleAppointmentCommand
            {
                Identifier = "abc123",
                NewStartUtc = Proposed,
                RequestedByEmail = ProviderEmail
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        providers.Verify(
            p => p.ChangeEmbeddedAppointmentScheduleAsync(
                ProviderEmail, "abc123", Proposed, Proposed.AddMinutes(45),
                It.Is<DateTime?>(previous => previous == Start)),
            Times.Once);
    }

    /// <summary>
    /// A customer cannot move a session outright — that route is the provider's. The refusal has to happen before
    /// anything is written, so no embedded write is attempted either.
    /// </summary>
    [Fact]
    public async Task ACustomerCannotMoveASessionOutright()
    {
        var appointment = Appointment();
        var providers = new Mock<IProviderService>();

        var handler = new RescheduleAppointmentCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Bookings(appointment).Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(
            new RescheduleAppointmentCommand
            {
                Identifier = "abc123",
                NewStartUtc = Proposed,
                RequestedByEmail = CustomerEmail
            },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        providers.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Approving one's own proposal is refused, or the ask-and-agree shape is decorative.
    /// </summary>
    [Fact]
    public async Task TheProposerCannotAnswerTheirOwnRequest()
    {
        var appointment = Appointment(
            AppointmentStatus.RescheduleRequested, proposedStart: Proposed, proposedBy: CustomerEmail);
        var providers = new Mock<IProviderService>();

        var handler = new AnswerRescheduleCommandHandler(
            Mock.Of<IMediator>(), providers.Object, Bookings(appointment).Object,
            Mock.Of<IEventStore>(), Mock.Of<INotificationDispatcher>());

        var result = await handler.Handle(
            new AnswerRescheduleCommand
            {
                Identifier = "abc123",
                Approve = true,
                AnsweredByEmail = CustomerEmail
            },
            CancellationToken.None);

        Assert.True(result.IsFailed);
        providers.VerifyNoOtherCalls();
    }
}
