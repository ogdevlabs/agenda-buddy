using System;
using AgendaBuddy.Library.Entities;
using Xunit;

namespace AgendaBuddy.Booking.Tests.Lifecycle;

/// <summary>
/// A session completes because its time has passed, not because somebody said so.
/// </summary>
/// <remarks>
/// Completion used to be a button on the appointment page. That made "Completed" a chore the provider had to
/// remember, and a session nobody pressed it for stayed <c>Booked</c> for ever — so reporting counted
/// long-finished work as outstanding and a customer's history never filled up. The button is gone;
/// <c>AppointmentAutoCompletionService</c> applies the rule below.
/// </remarks>
public class AutoCompletionTest
{
    private static readonly DateTime NowUtc = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private static AppointmentEntity Appointment(
        AppointmentStatus status,
        DateTime start,
        DateTime? end = null,
        int? durationMinutes = 45) =>
        new()
        {
            EmailProvider = "coach@example.com",
            EmailCustomer = "ada@example.com",
            Start = start,
            End = end ?? start.AddMinutes(durationMinutes ?? 45),
            AppointmentStatus = status,
            ServiceDurationMinutes = durationMinutes
        };

    [Fact]
    public void ASessionThatHasFinishedCompletes()
    {
        var appointment = Appointment(AppointmentStatus.Booked, NowUtc.AddHours(-2));

        Assert.True(appointment.ShouldAutoCompleteAt(NowUtc));
    }

    [Fact]
    public void ASessionStillToComeDoesNot()
    {
        var appointment = Appointment(AppointmentStatus.Booked, NowUtc.AddHours(2));

        Assert.False(appointment.ShouldAutoCompleteAt(NowUtc));
    }

    /// <summary>
    /// A session in progress is not finished. The END is what matters, not the start — completing on the start
    /// would mark a 45-minute session done the moment it began.
    /// </summary>
    [Fact]
    public void ASessionUnderWayDoesNot()
    {
        var appointment = Appointment(AppointmentStatus.Booked, NowUtc.AddMinutes(-10));

        Assert.False(appointment.ShouldAutoCompleteAt(NowUtc));
    }

    [Fact]
    public void ASessionEndingExactlyNowCompletes()
    {
        var appointment = Appointment(AppointmentStatus.Booked, NowUtc.AddMinutes(-45));

        Assert.Equal(NowUtc, appointment.EffectiveEndUtc);
        Assert.True(appointment.ShouldAutoCompleteAt(NowUtc));
    }

    /// <summary>
    /// Only a BOOKED session completes, and each exclusion is a different statement.
    /// </summary>
    /// <remarks>
    /// <c>Requested</c> was never agreed to, so a past request is an expired ask rather than work delivered.
    /// <c>Cancelled</c> was called off. <c>Completed</c> already is. And <c>RescheduleRequested</c> is left alone
    /// deliberately: completing it would silently answer a proposal nobody responded to.
    /// </remarks>
    [Theory]
    [InlineData(AppointmentStatus.Requested)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.RescheduleRequested)]
    public void OnlyABookedSessionCompletes(AppointmentStatus status)
    {
        var appointment = Appointment(status, NowUtc.AddDays(-3));

        Assert.False(appointment.ShouldAutoCompleteAt(NowUtc));
    }

    /// <summary>
    /// A row whose stored end is not after its start — older documents predate <c>End</c> being required — is
    /// treated as one session of its recorded length, not as zero-length. Zero-length would complete it the
    /// instant it began.
    /// </summary>
    [Fact]
    public void ARowWithNoUsableEndFallsBackToItsRecordedLength()
    {
        var start = NowUtc.AddMinutes(-20);
        var appointment = Appointment(AppointmentStatus.Booked, start, end: start, durationMinutes: 45);

        Assert.Equal(start.AddMinutes(45), appointment.EffectiveEndUtc);
        Assert.False(appointment.ShouldAutoCompleteAt(NowUtc));

        // …and it does complete once that length has actually elapsed.
        Assert.True(appointment.ShouldAutoCompleteAt(NowUtc.AddMinutes(30)));
    }

    /// <summary>
    /// A row with neither a usable end nor a recorded length gets the same 60-minute default the availability
    /// calculator assumes, rather than being treated as instantaneous.
    /// </summary>
    [Fact]
    public void ARowWithNoLengthAtAllGetsTheDefaultHour()
    {
        var start = NowUtc.AddMinutes(-30);
        var appointment = Appointment(AppointmentStatus.Booked, start, end: start, durationMinutes: null);

        Assert.Equal(start.AddHours(1), appointment.EffectiveEndUtc);
        Assert.False(appointment.ShouldAutoCompleteAt(NowUtc));
        Assert.True(appointment.ShouldAutoCompleteAt(NowUtc.AddHours(1)));
    }

    /// <summary>
    /// The rule and the transition agree: everything <see cref="AppointmentEntity.ShouldAutoCompleteAt"/> selects
    /// must actually be completable, or the service would pick up rows it cannot transition and retry them for
    /// ever.
    /// </summary>
    [Fact]
    public void EverythingTheRuleSelectsCanActuallyBeCompleted()
    {
        var appointment = Appointment(AppointmentStatus.Booked, NowUtc.AddDays(-1));
        Assert.True(appointment.ShouldAutoCompleteAt(NowUtc));

        appointment.TransitionTo(AppointmentStatus.Completed);

        Assert.Equal(AppointmentStatus.Completed, appointment.AppointmentStatus);
        Assert.Equal("Appointment Completed", appointment.AppointmentDescription);

        // And it is no longer selected, so a pass cannot loop on the same row.
        Assert.False(appointment.ShouldAutoCompleteAt(NowUtc));
    }

    /// <summary>
    /// The times are compared in UTC regardless of the <c>Kind</c> a row was read back with, so the rule is not
    /// sensitive to the machine's offset.
    /// </summary>
    [Fact]
    public void TheComparisonIsUtcRegardlessOfTheStoredKind()
    {
        var start = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Unspecified);
        var appointment = Appointment(AppointmentStatus.Booked, start);

        Assert.True(appointment.ShouldAutoCompleteAt(NowUtc));
    }
}
