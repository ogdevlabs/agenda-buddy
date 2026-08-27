using System;
using AgendaBuddy.Library.Entities;
using Xunit;

namespace AgendaBuddy.Booking.Tests.Lifecycle;

/// <summary>
/// F-014 AC-14 / threat T-203: the transition rules become the only path to a status change.
/// </summary>
/// <remarks>
/// <para>
/// <c>AppointmentLifecycleTest</c> beside this file already covered <c>Book()</c> and <c>Complete()</c>, and
/// it passed throughout — while <b>neither method was called anywhere in production</b>. What ran instead was
/// <c>appointment.AppointmentStatus = appointmentEntity.AppointmentStatus</c> in
/// <c>UpdateAppointmentCommandHandler</c>, copying whatever the client sent. So the rules were tested and
/// unreachable at the same time, which is the same shape as the five services F-014 exists to wire: covered
/// code that nothing calls.
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
    [InlineData(AppointmentStatus.Cancelled)]
    public void T203_StatesOutsideTheGraphAreUnreachable(AppointmentStatus target)
    {
        // Requested is the initial state and nothing transitions BACK to it. Confirmed is only ever produced
        // on a Calendar projection, and Cancelled is never persisted because cancellation deletes the
        // document — so neither has a meaning to transition into. A state added to the enum without a method
        // on the entity is unreachable by construction, which is the point of routing through Book()/Complete()
        // instead of a table in a handler (ADR D-4).
        var appointment = Appointment();

        Assert.Throws<InvalidOperationException>(() => appointment.TransitionTo(target));
        Assert.Equal(AppointmentStatus.Requested, appointment.AppointmentStatus);
    }

    [Fact]
    public void TheTransitionMessageNamesTheLegalTargets()
    {
        // A 409 body is what a client sees when it gets this wrong, so the message has to be actionable.
        var error = Assert.Throws<InvalidOperationException>(
            () => Appointment().TransitionTo(AppointmentStatus.Cancelled));

        Assert.Contains("Booked", error.Message, StringComparison.Ordinal);
        Assert.Contains("Completed", error.Message, StringComparison.Ordinal);
    }
}
