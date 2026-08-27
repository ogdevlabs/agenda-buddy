using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Provider.Tests.Auth;

public class ProviderOnboardingAuthTest
{
    [Fact]
    public void OwnershipGuard_AssertOwner_SameEmail_DoesNotThrow()
    {
        var user = MakeUser("provider@example.com", "Provider");
        var ex = Record.Exception(() => OwnershipGuard.AssertOwner(user, "provider@example.com"));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertOwner_DifferentEmail_ThrowsForbidden()
    {
        var user = MakeUser("provider@example.com", "Provider");
        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertOwner(user, "other@example.com"));
    }

    [Fact]
    public void OwnershipGuard_AssertOwner_CaseInsensitive_DoesNotThrow()
    {
        var user = MakeUser("PROVIDER@EXAMPLE.COM", "Provider");
        var ex = Record.Exception(() => OwnershipGuard.AssertOwner(user, "provider@example.com"));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertRole_CorrectRole_DoesNotThrow()
    {
        var user = MakeUser("provider@example.com", "Provider");
        var ex = Record.Exception(() => OwnershipGuard.AssertRole(user, "Provider"));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertRole_WrongRole_ThrowsForbidden()
    {
        var user = MakeUser("customer@example.com", "Customer");
        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertRole(user, "Provider"));
    }

    [Fact]
    public void OwnershipGuard_AssertOwnerAny_MatchesFirst_DoesNotThrow()
    {
        var user = MakeUser("p@example.com", "Provider");
        var ex = Record.Exception(() =>
            OwnershipGuard.AssertOwnerAny(user, "p@example.com", "other@example.com"));
        Assert.Null(ex);
    }

    [Fact]
    public void OwnershipGuard_AssertOwnerAny_NoMatch_ThrowsForbidden()
    {
        var user = MakeUser("p@example.com", "Provider");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwnerAny(user, "a@example.com", "b@example.com"));
    }

    [Fact]
    public void ForbiddenException_DefaultMessage_Contains403Context()
    {
        var ex = new ForbiddenException();
        Assert.Equal(403, ex.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void ProviderEntity_RequiredFields_Validated()
    {
        var entity = new ProviderEntity
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@example.com"
        };
        Assert.Equal("alice@example.com", entity.Email);
        Assert.Equal("Alice", entity.FirstName);
    }

    private static ClaimsPrincipal MakeUser(string email, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Role, role)
        ], "Test"));
}
