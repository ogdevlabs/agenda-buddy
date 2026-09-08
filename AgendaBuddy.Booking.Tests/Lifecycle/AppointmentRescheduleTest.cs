using System;
using AgendaBuddy.Library.Entities;
using Xunit;

namespace AgendaBuddy.Booking.Tests.Lifecycle;

/// <summary>
/// The reschedule-proposal rules on <see cref="AppointmentEntity"/>.
/// </summary>
/// <remarks>
/// A proposal is the first state in this product that holds two times at once — where the session IS and where
/// somebody has asked to move it. Every test here defends the distinction: until the other party answers, the
/// appointment has not moved, and every calendar must keep showing it where it was.
/// </remarks>
public class AppointmentRescheduleTest
{
    private static readonly DateTime NowUtc = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private static AppointmentEntity Booked(int durationMinutes = 60)
    {
        var start = NowUtc.AddDays(3);
        return new AppointmentEntity
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "ada@example.com",
            Start = start,
            End = start.AddMinutes(durationMinutes),
            AppointmentStatus = AppointmentStatus.Booked,
            ServiceDurationMinutes = durationMinutes
        };
    }

    [Fact]
    public void RequestReschedule_RecordsTheProposalWithoutMovingTheAppointment()
    {
        var appointment = Booked();
        var originalStart = appointment.Start;
        var originalEnd = appointment.End;
        var proposed = NowUtc.AddDays(4);

        appointment.RequestReschedule(proposed, appointment.EmailCustomer, NowUtc);

        Assert.Equal(AppointmentStatus.RescheduleRequested, appointment.AppointmentStatus);
        Assert.Equal(proposed, appointment.ProposedStart);
        Assert.Equal(appointment.EmailCustomer, appointment.ProposedBy);
        Assert.True(appointment.HasPendingReschedule);
        Assert.True(appointment.ProposedByCustomer);

        // The point of the whole design: a proposal is not a move.
        Assert.Equal(originalStart, appointment.Start);
        Assert.Equal(originalEnd, appointment.End);
        Assert.Null(appointment.PreviousStart);
    }

    [Fact]
    public void RequestReschedule_RefreshesTheHumanReadableDescription()
    {
        var appointment = Booked();

        appointment.RequestReschedule(NowUtc.AddDays(4), appointment.EmailProvider, NowUtc);

        Assert.Equal("Reschedule Requested", appointment.AppointmentDescription);
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void RequestReschedule_IsRefusedUnlessTheAppointmentIsBooked(AppointmentStatus status)
    {
        var appointment = Booked();
        appointment.AppointmentStatus = status;

        Assert.Throws<InvalidOperationException>(
            () => appointment.RequestReschedule(NowUtc.AddDays(4), appointment.EmailCustomer, NowUtc));
    }

    /// <summary>
    /// A second proposal is refused rather than overwriting the first, so neither party can end up answering a
    /// proposal the other has already replaced.
    /// </summary>
    [Fact]
    public void RequestReschedule_Twice_IsRefusedAndKeepsTheFirstProposal()
    {
        var appointment = Booked();
        var first = NowUtc.AddDays(4);
        appointment.RequestReschedule(first, appointment.EmailCustomer, NowUtc);

        var exception = Assert.Throws<InvalidOperationException>(
            () => appointment.RequestReschedule(NowUtc.AddDays(5), appointment.EmailProvider, NowUtc));

        Assert.Contains("awaiting an answer", exception.Message);
        Assert.Equal(first, appointment.ProposedStart);
        Assert.Equal(appointment.EmailCustomer, appointment.ProposedBy);
    }

    [Fact]
    public void RequestReschedule_InThePast_IsRefused()
    {
        var appointment = Booked();

        Assert.Throws<InvalidOperationException>(
            () => appointment.RequestReschedule(NowUtc.AddHours(-1), appointment.EmailCustomer, NowUtc));
    }

    [Fact]
    public void RequestReschedule_ToTheTimeAlreadyBooked_IsRefused()
    {
        var appointment = Booked();

        Assert.Throws<InvalidOperationException>(
            () => appointment.RequestReschedule(appointment.Start, appointment.EmailCustomer, NowUtc));
    }

    /// <summary>
    /// A proposal with nobody recorded as having made it cannot be answered — there is no way to tell which
    /// party owes the reply.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequestReschedule_WithNoProposer_IsRefused(string? proposedBy)
    {
        var appointment = Booked();

        // ThrowsAny, because null yields ArgumentNullException and blank yields ArgumentException — the
        // distinction is not one a caller acts on.
        Assert.ThrowsAny<ArgumentException>(
            () => appointment.RequestReschedule(NowUtc.AddDays(4), proposedBy!, NowUtc));
    }

    [Fact]
    public void ApproveReschedule_MovesTheAppointmentAndRecordsWhereItWas()
    {
        var appointment = Booked();
        var originalStart = appointment.Start;
        var proposed = NowUtc.AddDays(4);
        appointment.RequestReschedule(proposed, appointment.EmailCustomer, NowUtc);

        appointment.ApproveReschedule();

        Assert.Equal(AppointmentStatus.Booked, appointment.AppointmentStatus);
        Assert.Equal(proposed, appointment.Start);
        Assert.Equal(originalStart, appointment.PreviousStart);
        Assert.Null(appointment.ProposedStart);
        Assert.Null(appointment.ProposedBy);
        Assert.False(appointment.HasPendingReschedule);
    }

    /// <summary>
    /// Approving a move must not silently change how long the session is. The length is carried across rather
    /// than recomputed, so a 90-minute session stays 90 minutes wherever it lands.
    /// </summary>
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    public void ApproveReschedule_PreservesTheSessionLength(int durationMinutes)
    {
        var appointment = Booked(durationMinutes);
        appointment.RequestReschedule(NowUtc.AddDays(4), appointment.EmailProvider, NowUtc);

        appointment.ApproveReschedule();

        Assert.Equal(durationMinutes, (appointment.End - appointment.Start).TotalMinutes);
    }

    [Fact]
    public void DeclineReschedule_LeavesTheAppointmentExactlyWhereItWas()
    {
        var appointment = Booked();
        var originalStart = appointment.Start;
        var originalEnd = appointment.End;
        appointment.RequestReschedule(NowUtc.AddDays(4), appointment.EmailCustomer, NowUtc);

        appointment.DeclineReschedule();

        Assert.Equal(AppointmentStatus.Booked, appointment.AppointmentStatus);
        Assert.Equal(originalStart, appointment.Start);
        Assert.Equal(originalEnd, appointment.End);
        Assert.Null(appointment.ProposedStart);
        Assert.Null(appointment.ProposedBy);

        // Nothing moved, so there is no previous time to record. A declined proposal must not read afterwards
        // as though a reschedule happened.
        Assert.Null(appointment.PreviousStart);
    }

    [Theory]
    [InlineData(AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Completed)]
    public void ApproveOrDecline_WithNoProposalOutstanding_IsRefused(AppointmentStatus status)
    {
        var appointment = Booked();
        appointment.AppointmentStatus = status;

        Assert.Throws<InvalidOperationException>(() => appointment.ApproveReschedule());
        Assert.Throws<InvalidOperationException>(() => appointment.DeclineReschedule());
    }

    /// <summary>
    /// A status of <c>RescheduleRequested</c> with no proposed time is not something any caller should have to
    /// defend against, but if a stored row ever carries it, approving must refuse rather than move the
    /// appointment to <c>null</c>.
    /// </summary>
    [Fact]
    public void ApproveReschedule_WithTheStatusButNoProposedTime_IsRefused()
    {
        var appointment = Booked();
        appointment.AppointmentStatus = AppointmentStatus.RescheduleRequested;

        Assert.Throws<InvalidOperationException>(() => appointment.ApproveReschedule());
    }

    /// <summary>
    /// A pending proposal must not trap the appointment: either party can still cancel, and cancelling drops the
    /// proposal because there is nothing left to move.
    /// </summary>
    [Fact]
    public void Cancel_IsStillAllowedWhileAProposalIsOutstanding()
    {
        var appointment = Booked();
        appointment.RequestReschedule(NowUtc.AddDays(4), appointment.EmailCustomer, NowUtc);

        appointment.Cancel();

        Assert.Equal(AppointmentStatus.Cancelled, appointment.AppointmentStatus);
        Assert.Null(appointment.ProposedStart);
        Assert.Null(appointment.ProposedBy);
        Assert.False(appointment.HasPendingReschedule);
    }

    /// <summary>
    /// <see cref="AppointmentEntity.TransitionTo"/> takes a status and nothing else, so it cannot express a
    /// proposal. Allowing it would let a caller declare a pending reschedule carrying no proposed time — and
    /// <c>POST /appointments/{identifier}/status</c> dispatches straight into it.
    /// </summary>
    [Fact]
    public void TransitionTo_RescheduleRequested_IsRefusedAndSaysWhatToUseInstead()
    {
        var appointment = Booked();

        var exception = Assert.Throws<InvalidOperationException>(
            () => appointment.TransitionTo(AppointmentStatus.RescheduleRequested));

        Assert.Contains("RequestReschedule", exception.Message);
        Assert.Equal(AppointmentStatus.Booked, appointment.AppointmentStatus);
    }

    /// <summary>
    /// The integer is what MongoDB stores, so a member inserted anywhere but the end silently reinterprets every
    /// stored appointment. This is the assertion that makes the enum append-only in practice rather than by
    /// comment.
    /// </summary>
    [Theory]
    [InlineData(AppointmentStatus.Requested, 0)]
    [InlineData(AppointmentStatus.Booked, 1)]
    [InlineData(AppointmentStatus.Completed, 2)]
    [InlineData(AppointmentStatus.Confirmed, 3)]
    [InlineData(AppointmentStatus.Cancelled, 4)]
    [InlineData(AppointmentStatus.RescheduleRequested, 5)]
    public void AppointmentStatus_IntegersArePinned(AppointmentStatus status, int expected) =>
        Assert.Equal(expected, (int)status);

    [Fact]
    public void AppointmentStatus_HasNoUnaccountedMembers() =>
        Assert.Equal(6, Enum.GetValues<AppointmentStatus>().Length);
}
