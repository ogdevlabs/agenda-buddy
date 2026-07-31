using System;
using System.Linq;
using System.Security.Claims;
using Xunit;
using Library.Tools;

namespace Common.Tests.Tools;

public class OwnershipGuardTest
{
    private static ClaimsPrincipal MakePrincipal(string? sub)
    {
        var claims = sub is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, sub) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal MakePrincipalWithRole(string sub, string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim(ClaimTypes.Role, role)
        }, "test"));
    }

    [Fact]
    public void AssertOwner_WhenSubMatchesEmail_DoesNotThrow()
    {
        var principal = MakePrincipal("provider@example.com");
        // Should not throw
        OwnershipGuard.AssertOwner(principal, "provider@example.com");
    }

    [Fact]
    public void AssertOwner_WhenSubMatchesDifferentCase_DoesNotThrow()
    {
        var principal = MakePrincipal("Provider@Example.COM");
        OwnershipGuard.AssertOwner(principal, "provider@example.com");
    }

    [Fact]
    public void AssertOwner_WhenSubDoesNotMatchEmail_ThrowsForbiddenException()
    {
        var principal = MakePrincipal("attacker@evil.com");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwner(principal, "victim@example.com"));
    }

    [Fact]
    public void AssertOwner_WhenPrincipalHasNoSubClaim_ThrowsForbiddenException()
    {
        var principal = MakePrincipal(null);
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwner(principal, "victim@example.com"));
    }

    [Fact]
    public void AssertOwnerAny_WhenSubMatchesFirstEmail_DoesNotThrow()
    {
        var principal = MakePrincipal("provider@example.com");
        OwnershipGuard.AssertOwnerAny(principal, "provider@example.com", "customer@example.com");
    }

    [Fact]
    public void AssertOwnerAny_WhenSubMatchesSecondEmail_DoesNotThrow()
    {
        var principal = MakePrincipal("customer@example.com");
        OwnershipGuard.AssertOwnerAny(principal, "provider@example.com", "customer@example.com");
    }

    [Fact]
    public void AssertOwnerAny_WhenSubMatchesNeitherEmail_ThrowsForbiddenException()
    {
        var principal = MakePrincipal("attacker@evil.com");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertOwnerAny(principal, "provider@example.com", "customer@example.com"));
    }

    [Fact]
    public void AssertRole_WhenPrincipalHasMatchingRole_DoesNotThrow()
    {
        var principal = MakePrincipalWithRole("provider@example.com", "Provider");
        OwnershipGuard.AssertRole(principal, "Provider");
    }

    [Fact]
    public void AssertRole_WhenPrincipalLacksRole_ThrowsForbiddenException()
    {
        var principal = MakePrincipalWithRole("customer@example.com", "Customer");
        Assert.Throws<ForbiddenException>(() =>
            OwnershipGuard.AssertRole(principal, "Provider"));
    }

    [Fact]
    public void ForbiddenException_HasCorrect403StatusCode()
    {
        var ex = new ForbiddenException();
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public void ForbiddenException_DefaultMessageIsUsable()
    {
        var ex = new ForbiddenException();
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}
