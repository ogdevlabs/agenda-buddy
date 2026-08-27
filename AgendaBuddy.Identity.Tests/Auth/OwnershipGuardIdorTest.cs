using System.Security.Claims;
using AgendaBuddy.Library.Tools;
using Xunit;

namespace AgendaBuddy.Identity.Tests.Auth;

/// <summary>
/// IDOR tests for OwnershipGuard.
/// Threat-model T-004: a valid token for user A must not grant access to resources owned by user B.
/// PRD AC 14-18.
/// </summary>
public class OwnershipGuardIdorTest
{
    private static ClaimsPrincipal MakeUser(string email, string role = "Provider")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Role, role),
        }, "test"));
    }

    // --- Booking: provider/customer ownership ---

    [Fact]
    public void Booking_ProviderTokenForSameAppointment_Passes()
    {
        var providerA = MakeUser("providerA@example.com", "Provider");
        OwnershipGuard.AssertOwnerAny(providerA, "providerA@example.com", "customerX@example.com");
    }

    [Fact]
    public void Booking_CustomerTokenForSameAppointment_Passes()
    {
        var customerX = MakeUser("customerX@example.com", "Customer");
        OwnershipGuard.AssertOwnerAny(customerX, "providerA@example.com", "customerX@example.com");
    }

    [Fact]
    public void Booking_ProviderTokenForOtherAppointment_Throws403()
    {
        var providerB = MakeUser("providerB@example.com", "Provider");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwnerAny(providerB, "providerA@example.com", "customerX@example.com"));
    }

    [Fact]
    public void Booking_CustomerTokenForOtherAppointment_Throws403()
    {
        var customerY = MakeUser("customerY@example.com", "Customer");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwnerAny(customerY, "providerA@example.com", "customerX@example.com"));
    }

    // --- Calendar: provider availability ---

    [Fact]
    public void Calendar_ProviderTokenForOwnCalendar_Passes()
    {
        var provider = MakeUser("provider@example.com", "Provider");
        OwnershipGuard.AssertOwner(provider, "provider@example.com");
    }

    [Fact]
    public void Calendar_ProviderTokenForOtherCalendar_Throws403()
    {
        var providerB = MakeUser("providerB@example.com", "Provider");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwner(providerB, "providerA@example.com"));
    }

    // --- Provider profile ---

    [Fact]
    public void Provider_UpdateOwnProfile_Passes()
    {
        var provider = MakeUser("provider@example.com", "Provider");
        OwnershipGuard.AssertOwner(provider, "provider@example.com");
    }

    [Fact]
    public void Provider_UpdateOtherProfile_Throws403()
    {
        var providerB = MakeUser("providerB@example.com", "Provider");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwner(providerB, "providerA@example.com"));
    }

    // --- Customer profile ---

    [Fact]
    public void Customer_UpdateOwnProfile_Passes()
    {
        var customer = MakeUser("customer@example.com", "Customer");
        OwnershipGuard.AssertOwner(customer, "customer@example.com");
    }

    [Fact]
    public void Customer_UpdateOtherProfile_Throws403()
    {
        var customerB = MakeUser("customerB@example.com", "Customer");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwner(customerB, "customerA@example.com"));
    }

    // --- Customer write endpoint: requires Provider role ---

    [Fact]
    public void CustomerWriteEndpoint_ProviderToken_Passes()
    {
        var provider = MakeUser("provider@example.com", "Provider");
        OwnershipGuard.AssertRole(provider, "Provider");
    }

    [Fact]
    public void CustomerWriteEndpoint_CustomerToken_Throws403()
    {
        var customer = MakeUser("customer@example.com", "Customer");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertRole(customer, "Provider"));
    }

    // --- NoSQL injection path: email validation ---

    [Fact]
    public void Register_NoSqlInjectionEmail_IsInvalidEmail()
    {
        // Validates that {"":{""}} or operator-style emails fail EmailAddressAttribute validation
        var emailValidator = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();

        Assert.False(emailValidator.IsValid("{\"\":\"\"}")); // { "": "" }
        Assert.False(emailValidator.IsValid("{$ne: null}")); // MongoDB operator
        Assert.False(emailValidator.IsValid("'; DROP TABLE--")); // SQL-style
    }
}
