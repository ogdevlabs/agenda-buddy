using System.Security.Claims;
using AgendaBuddy.EventAndCommands.Persistence;

namespace AgendaBuddy.EventsAndCommands.Tests.Persistence;

/// <summary>
/// Pins <see cref="AuditActor"/> — F-016-T18, the attribution half of AC-24 (threat <b>T-005</b>).
/// </summary>
/// <remarks>
/// <para>
/// <c>15-cqrs-and-messaging.md:215</c>: <i>"No actor, no correlation, no request id. The audit trail cannot
/// answer 'who did this'."</i> Until F-016 these endpoints had no authenticated caller to record, so the
/// field had nothing to hold. Now they do — and reducing the payload without adding attribution would
/// leave the trail <b>less</b> useful for incident response than the PII dump was, which is the argument
/// ADR-027 rests on.
/// </para>
/// <para>
/// Resolution is a pure function of a <see cref="ClaimsPrincipal"/> so it can be tested without a
/// container, a request, or a mocking framework — <c>EventsAndCommands.Tests</c> has no Moq, and adding a
/// package to reach behind <c>IMongoCollection</c> would be a poor trade for one field. <c>EventStore</c>
/// supplies the principal from <c>IHttpContextAccessor</c>; the decision about what counts as an actor
/// lives here.
/// </para>
/// </remarks>
public class AuditActorTest
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void T005_From_APrincipalCarryingASubject_ReturnsThatSubject()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "owner@example.com"));

        Assert.Equal("owner@example.com", AuditActor.From(principal));
    }

    [Fact]
    public void From_NoPrincipal_ReturnsNull()
    {
        // A hosted service or a background job writes audit events with no request in flight. Null is the
        // correct, honest answer -- which is why Event.Actor is nullable and needs no backfill.
        Assert.Null(AuditActor.From(null));
    }

    [Fact]
    public void From_AnAnonymousPrincipal_ReturnsNull()
    {
        // Every route that reaches a query handler is anonymous until F-016-T12 authenticates the five
        // PII GETs, so this is the live case today, not an edge case.
        Assert.Null(AuditActor.From(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void From_APrincipalWithARoleButNoSubject_ReturnsNull()
    {
        // The threat T-001 token shape. Authenticated, but with nothing to attribute to -- so it must not
        // be attributed to the empty string or to the role.
        var principal = PrincipalWith(new Claim(ClaimTypes.Role, "Provider"));

        Assert.Null(AuditActor.From(principal));
    }

    [Fact]
    public void From_APrincipalWithABlankSubject_ReturnsNull()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "   "));

        Assert.Null(AuditActor.From(principal));
    }
}
