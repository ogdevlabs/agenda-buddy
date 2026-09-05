using System;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Booking.Tests.Lifecycle;

/// <summary>
/// The transition rules are the only path to a status change.
/// </summary>
/// <remarks>
/// <para>
/// <c>AppointmentLifecycleTest</c> beside this file already covered <c>Book()</c> and <c>Complete()</c>, and
/// it passed throughout — while <b>neither method was called anywhere in production</b>. What ran instead was
/// <c>appointment.AppointmentStatus = appointmentEntity.AppointmentStatus</c> in
/// <c>UpdateAppointmentCommandHandler</c>, copying whatever the client sent. So the rules were tested and
/// unreachable at the same time: covered code that nothing calls.
/// </para>
/// <para>
/// These tests cover <see cref="AppointmentEntity.TransitionTo"/>, the method the new route goes through.
/// </para>
/// </remarks>
public class AppointmentStatusTransitionTest
{
    private static AppointmentEntity Appointment(AppointmentStatus status = AppointmentStatus.Requested) =>
        new()
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "ada@example.com",
            AppointmentStatus = status
        };

    [Fact]
    public void T203_RequestedToBooked_IsLegal_AndRefreshesTheDescription()
    {
        var appointment = Appointment();

        appointment.TransitionTo(AppointmentStatus.Booked);

        Assert.Equal(AppointmentStatus.Booked, appointment.AppointmentStatus);
        // The description is a rendering of the status, so it is derived rather than accepted from a caller —
        // otherwise the two could disagree and the client would render the stale one.
        Assert.Equal("Appointment Booked", appointment.AppointmentDescription);
    }

    [Fact]
    public void T203_BookedToCompleted_IsLegal()
    {
        var appointment = Appointment(AppointmentStatus.Booked);

        appointment.TransitionTo(AppointmentStatus.Completed);

        Assert.Equal(AppointmentStatus.Completed, appointment.AppointmentStatus);
        Assert.Equal("Appointment Completed", appointment.AppointmentDescription);
    }

    [Fact]
    public void T203_RequestedStraightToCompleted_IsRefused_AndChangesNothing()
    {
        // The forgery this feature exists to stop: a caller asserting that work was delivered on an
        // appointment created a second earlier.
        var appointment = Appointment();

        Assert.Throws<InvalidOperationException>(() => appointment.TransitionTo(AppointmentStatus.Completed));

        Assert.Equal(AppointmentStatus.Requested, appointment.AppointmentStatus);
    }

    [Fact]
    public void T203_CompletedCannotBeReopened()
    {
        // Both directions of the same rule: a completed appointment cannot go back to Booked, which would
        // erase it from the provider's completed count.
        var appointment = Appointment(AppointmentStatus.Completed);

        Assert.Throws<InvalidOperationException>(() => appointment.TransitionTo(AppointmentStatus.Booked));
        Assert.Throws<InvalidOperationException>(() => appointment.TransitionTo(AppointmentStatus.Completed));

        Assert.Equal(AppointmentStatus.Completed, appointment.AppointmentStatus);
    }

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Confirmed)]
    public void T203_StatesOutsideTheGraphAreUnreachable(AppointmentStatus target)
    {
        // Requested is the initial state and nothing transitions BACK to it. Confirmed is only ever produced
        // on a Calendar projection, so it has no meaning to transition into. A state added to the enum without
        // a method on the entity is unreachable by construction, which is the point of routing through
        // Book()/Complete()/Cancel() instead of a table in a handler (ADR D-4).
        //
        // Cancelled USED to be in this list, because cancellation deleted the document rather than setting a
        // status. It is now a legal target -- see the cancellation tests below.
        var appointment = Appointment();

        Assert.Throws<InvalidOperationException>(() => appointment.TransitionTo(target));
        Assert.Equal(AppointmentStatus.Requested, appointment.AppointmentStatus);
    }

    [Fact]
    public void TheTransitionMessageNamesTheLegalTargets()
    {
        // A 409 body is what a client sees when it gets this wrong, so the message has to be actionable.
        var error = Assert.Throws<InvalidOperationException>(
            () => Appointment().TransitionTo(AppointmentStatus.Confirmed));

        Assert.Contains("Booked", error.Message, StringComparison.Ordinal);
        Assert.Contains("Completed", error.Message, StringComparison.Ordinal);
        Assert.Contains("Cancelled", error.Message, StringComparison.Ordinal);
    }

    // ── Cancellation ────────────────────────────────────────────────────────────────────────────────
    // A soft delete: cancelling sets the status and keeps the record, so the slot frees up but the fact that
    // it was ever booked survives -- for reporting, and so a cancellation notification can still open it.

    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Booked)]
    public void EitherALiveRequestOrALiveBookingCanBeCancelled(AppointmentStatus from)
    {
        var appointment = Appointment(from);

        appointment.TransitionTo(AppointmentStatus.Cancelled);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.AppointmentStatus);
        // The description travels with the status, so the stored pair cannot disagree.
        Assert.Equal(
            EnumHelper<AppointmentStatus>.GetEnumDescription(AppointmentStatus.Cancelled),
            appointment.AppointmentDescription);
    }

    /// <summary>
    /// Completed work cannot be cancelled — it is history, and "cancel" is not a thing you can do to a session
    /// that was already delivered.
    /// </summary>
    [Fact]
    public void ACompletedAppointmentCannotBeCancelled()
    {
        var appointment = Appointment(AppointmentStatus.Completed);

        Assert.Throws<InvalidOperationException>(() => appointment.TransitionTo(AppointmentStatus.Cancelled));
        Assert.Equal(AppointmentStatus.Completed, appointment.AppointmentStatus);
    }

    // Nothing comes back from Cancelled. Rebooking is a new appointment, not a resurrection of this one.
    [Theory]
    [InlineData(AppointmentStatus.Booked)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void ACancelledAppointmentIsTerminal(AppointmentStatus target)
    {
        var appointment = Appointment(AppointmentStatus.Cancelled);

        Assert.Throws<InvalidOperationException>(() => appointment.TransitionTo(target));
        Assert.Equal(AppointmentStatus.Cancelled, appointment.AppointmentStatus);
    }
}
