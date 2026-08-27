using System;
using System.Security.Claims;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Tools;

namespace AgendaBuddy.Booking.Tests.Lifecycle;

public class AppointmentLifecycleTest
{
    // --- Status Transitions ---

    [Fact]
    public void Book_FromRequested_SetsStatusBooked()
    {
        var appt = MakeAppointment();
        appt.Book();
        Assert.Equal(AppointmentStatus.Booked, appt.AppointmentStatus);
    }

    [Fact]
    public void Book_FromBooked_ThrowsInvalidOperation()
    {
        var appt = MakeAppointment();
        appt.Book();
        Assert.Throws<InvalidOperationException>(() => appt.Book());
    }

    [Fact]
    public void Complete_FromBooked_SetsStatusCompleted()
    {
        var appt = MakeAppointment();
        appt.Book();
        appt.Complete();
        Assert.Equal(AppointmentStatus.Completed, appt.AppointmentStatus);
    }

    [Fact]
    public void Complete_FromRequested_ThrowsInvalidOperation()
    {
        var appt = MakeAppointment();
        Assert.Throws<InvalidOperationException>(() => appt.Complete());
    }

    [Fact]
    public void NewAppointment_DefaultStatus_IsRequested()
    {
        var appt = MakeAppointment();
        Assert.Equal(AppointmentStatus.Requested, appt.AppointmentStatus);
    }

    [Fact]
    public void NewAppointment_Identifier_IsNotEmpty()
    {
        var appt = MakeAppointment();
        Assert.False(string.IsNullOrWhiteSpace(appt.Identifier));
    }

    // --- Ownership ---

    [Fact]
    public void OwnershipGuard_AssertOwnerAny_ProviderCanBook()
    {
        var user = MakeUser("provider@example.com", "Provider");
        var appt = MakeAppointment("provider@example.com", "customer@example.com");
        var ex = Record.Exception(() =>
            OwnershipGuard.AssertOwnerAny(user, appt.EmailProvider, appt.EmailCustomer));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertOwnerAny_CustomerCanBook()
    {
        var user = MakeUser("customer@example.com", "Customer");
        var appt = MakeAppointment("provider@example.com", "customer@example.com");
        var ex = Record.Exception(() =>
            OwnershipGuard.AssertOwnerAny(user, appt.EmailProvider, appt.EmailCustomer));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertOwnerAny_ThirdParty_ThrowsForbidden()
    {
        var user = MakeUser("attacker@example.com", "Customer");
        var appt = MakeAppointment("provider@example.com", "customer@example.com");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwnerAny(user, appt.EmailProvider, appt.EmailCustomer));
    }

    private static AppointmentEntity MakeAppointment(
        string provider = "p@example.com",
        string customer = "c@example.com") =>
        new()
        {
            EmailProvider = provider,
            EmailCustomer = customer,
            Start = DateTime.UtcNow.AddHours(1),
            End = DateTime.UtcNow.AddHours(2)
        };

    private static ClaimsPrincipal MakeUser(string email, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Role, role)
        ], "Test"));
}
