using System.Security.Claims;

namespace EventAndCommands.Persistence;

/// <summary>
/// Decides who an audit record is attributed to.
/// </summary>
/// <remarks>
/// <para>
/// F-016-T18, the attribution half of AC-24 (threat <b>T-005</b>), ADR-027.
/// </para>
/// <para>
/// A pure function of a <see cref="ClaimsPrincipal"/>, deliberately. It keeps the "what counts as an
/// actor" decision testable without a request, a container or a mocking framework, and it keeps the rule
/// in one place rather than repeated at each write. <c>EventStore</c> supplies the principal.
/// </para>
/// <para>
/// <b>Null is a correct answer, not a failure.</b> A hosted service writing an event has no request; an
/// anonymous read has no subject; and a token carrying no <c>sub</c> claim (the threat T-001 shape) has
/// nothing to attribute to. In all three cases the honest record is "unattributed", which is why
/// <see cref="Event.Actor"/> is nullable and needs no backfill — the actor of a historical anonymous read
/// is genuinely unknown, so inventing one would be worse than leaving it empty.
/// </para>
/// </remarks>
public static class AuditActor
{
    /// <summary>
    /// The caller's subject, or <c>null</c> when there is no identifiable caller.
    /// </summary>
    public static string? From(ClaimsPrincipal? principal)
    {
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        // Whitespace is treated as absent: a blank actor reads as attribution when there is none.
        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }
}
