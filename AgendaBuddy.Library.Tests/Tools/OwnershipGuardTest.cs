using System;
using System.Linq;
using System.Security.Claims;
using Xunit;
using AgendaBuddy.Library.Tools;

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

    // ── F-016-T09 · threat T-001 (HIGH) · PRD AC-21 ──────────────────────────────────────────────
    //
    // AssertOwner compared with string.Equals(sub, entityEmail, OrdinalIgnoreCase) and guarded NEITHER
    // side against null. string.Equals(null, null) is TRUE, so a caller with no `sub` claim, checked
    // against an entity with no email, was granted OWNERSHIP.
    //
    // AssertOwnerAny had the guard all along (OwnershipGuard.cs:17 checks `sub is null` first).
    // AssertOwner did not. That asymmetry is the whole defect, and it is documented in this repo's own
    // public context catalog.
    //
    // Note what the pre-existing AssertOwner_WhenPrincipalHasNoSubClaim_ThrowsForbiddenException test
    // above does NOT cover: it passes a non-null "victim@example.com", so string.Equals(null, "victim")
    // is false and it throws for the wrong reason. It would have kept passing throughout.
    //
    // Why this had to be fixed in F-016 rather than deferred to F-021 (ADR-028): T11's response
    // projection selects owner-vs-non-owner with this exact primitive, and the null fall-through lands
    // on the OWNER branch -- returning the unprojected ProviderEntity with its full appointment book and
    // subscribed-customer list. Building T11 first would have shipped the bypass.

    [Fact]
    public void T001_AssertOwner_WhenNeitherSubNorEntityEmailIsKnown_Throws()
    {
        // The hole itself. Both sides null, string.Equals says "equal", and the guard used to pass.
        var principal = MakePrincipal(null);

        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertOwner(principal, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("victim@example.com")]
    public void T001_AssertOwner_WithNoSubClaim_ThrowsWhateverTheEntityEmailIs(string? entityEmail)
    {
        var principal = MakePrincipal(null);

        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertOwner(principal, entityEmail));
    }

    [Fact]
    public void T001_AssertOwner_WhenTheEntityHasNoEmail_ThrowsEvenForAValidCaller()
    {
        // The other direction, and it matters for T11: an entity with no email has no owner, so nobody
        // may be treated as its owner -- not even a perfectly authenticated caller.
        var principal = MakePrincipal("provider@example.com");

        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertOwner(principal, null));
    }

    [Fact]
    public void T001_AssertOwnerAndAssertOwnerAny_NowAgreeOnAMissingSubClaim()
    {
        // Pins the asymmetry closed. AssertOwnerAny always rejected a null sub; AssertOwner now does too.
        // If they ever diverge again, this fails rather than the divergence hiding until a projection
        // branches on it.
        var principal = MakePrincipal(null);

        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertOwner(principal, null));
        Assert.Throws<ForbiddenException>(() => OwnershipGuard.AssertOwnerAny(principal));
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
