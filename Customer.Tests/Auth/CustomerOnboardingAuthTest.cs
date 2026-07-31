using System.Security.Claims;
using Library.Entities;
using Library.Tools;

namespace Customer.Tests.Auth;

public class CustomerOnboardingAuthTest
{
    [Fact]
    public void OwnershipGuard_AssertOwner_SameEmail_DoesNotThrow()
    {
        var user = MakeUser("customer@example.com", "Customer");
        var ex = Record.Exception(() => OwnershipGuard.AssertOwner(user, "customer@example.com"));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertOwner_DifferentEmail_ThrowsForbidden()
    {
        var user = MakeUser("customer@example.com", "Customer");
        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertOwner(user, "other@example.com"));
    }

    [Fact]
    public void OwnershipGuard_AssertRole_Customer_DoesNotThrow()
    {
        var user = MakeUser("customer@example.com", "Customer");
        var ex = Record.Exception(() => OwnershipGuard.AssertRole(user, "Customer"));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertRole_ProviderOnCustomer_ThrowsForbidden()
    {
        var user = MakeUser("customer@example.com", "Customer");
        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertRole(user, "Provider"));
    }

    [Fact]
    public void CustomerEntity_DefaultSubscribedProviders_IsEmptyList()
    {
        var entity = new CustomerEntity();
        Assert.NotNull(entity.SubscribedProviderCollection);
    }

    [Fact]
    public void CustomerEntity_DefaultAppointmentCollection_IsEmptyList()
    {
        var entity = new CustomerEntity();
        Assert.NotNull(entity.AppointmentCollection);
    }

    private static ClaimsPrincipal MakeUser(string email, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Role, role)
        ], "Test"));
}
