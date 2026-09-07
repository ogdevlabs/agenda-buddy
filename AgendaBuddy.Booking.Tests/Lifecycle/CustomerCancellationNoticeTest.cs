using System;
using AgendaBuddy.Library.Entities;
using Xunit;

namespace AgendaBuddy.Booking.Tests.Lifecycle;

/// <summary>
/// The customer's cancellation notice period, as the appointment itself expresses it.
/// </summary>
/// <remarks>
/// The authoritative check is a clause in <c>BookingService.CancelAppointmentAsync</c>'s filter, joined to the
/// cancellable-status rule so the two cannot be raced apart. What lives here is the same rule as a readable
/// property — used to show the deadline BEFORE it passes, and to word the refusal when it has.
/// </remarks>
public class CustomerCancellationNoticeTest
{
    private static readonly DateTime NowUtc = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private static AppointmentEntity At(DateTime startUtc) => new()
    {
        EmailProvider = "coach@example.com",
        EmailCustomer = "ada@example.com",
        Start = startUtc,
        End = startUtc.AddHours(1),
        AppointmentStatus = AppointmentStatus.Booked
    };

    [Fact]
    public void TheNoticePeriodIsTwentyFourHours() =>
        Assert.Equal(24, AppointmentEntity.CustomerCancellationNoticeHours);

    [Fact]
    public void TheDeadlineIsTheNoticePeriodBeforeTheStart()
    {
        var start = NowUtc.AddDays(3);

        Assert.Equal(start.AddHours(-24), At(start).CustomerCancellationDeadlineUtc);
    }

    [Theory]
    [InlineData(72, true)]      // three days out
    [InlineData(25, true)]      // an hour inside the limit
    [InlineData(24, false)]     // exactly on it — the deadline is exclusive, so this is too late
    [InlineData(23, false)]
    [InlineData(1, false)]
    public void ACustomerMayCancelOnlyOutsideTheNoticePeriod(int hoursAhead, bool mayCancel) =>
        Assert.Equal(mayCancel, At(NowUtc.AddHours(hoursAhead)).CustomerMayCancelAt(NowUtc));

    [Fact]
    public void APastAppointmentIsNotCancellableByACustomer() =>
        Assert.False(At(NowUtc.AddHours(-1)).CustomerMayCancelAt(NowUtc));

    /// <summary>
    /// A row with no start — older documents and hand-built fixtures both have one — must not throw out of a
    /// property getter on a read path. Subtracting the notice period from <see cref="DateTime.MinValue"/>
    /// underflows, and an <c>ArgumentOutOfRangeException</c> there took down five cancel tests before it was
    /// clamped.
    /// </summary>
    [Fact]
    public void AnAppointmentWithNoStartDoesNotThrowAndIsNotCancellable()
    {
        var appointment = At(default);

        Assert.Equal(DateTime.MinValue, appointment.CustomerCancellationDeadlineUtc);
        Assert.False(appointment.CustomerMayCancelAt(NowUtc));
    }
}
