namespace AgendaBuddy.Booking.Validation;

/// <summary>
/// <c>NoteRequest</c> has ZERO <c>MiniValidator</c>
/// annotations today (<c>Booking/Requests/AppointmentExtrasRequests.cs</c>) -- the inline
/// <c>string.IsNullOrWhiteSpace(request?.Content)</c> check in <c>Program.cs</c>'s two note-content
/// routes is what actually enforces this. <see cref="NoteSpec"/> replaces that inline check.
/// </summary>
/// <remarks>
/// <b>Party Review found the originally-authored spec was wrong.</b> The T02 spike used
/// <c>.Required().NotEmpty()</c>, verified directly against the live Validot assembly to accept
/// <c>null</c>/<c>""</c> only -- a whitespace-only string ("   ") passes <c>.NotEmpty()</c>, which
/// would have silently let through exactly the input <c>IsNullOrWhiteSpace</c> rejects today. Fixed to
/// <c>.Required().NotWhiteSpace()</c>, confirmed byte-for-byte equivalent to
/// <c>!string.IsNullOrWhiteSpace(x)</c> against null/""/"   "/"x"/" x " before wiring it in.
/// </remarks>
/// <remarks>
/// <b>What this file used to also contain.</b> <c>StatusSpec</c> (for
/// <c>AppointmentStatusRequest</c>) and <c>PaymentSpec</c> (for <c>PaymentRequest</c>) were authored
/// at T02 as deliberate no-ops -- <c>AppointmentStatusRequest.Status</c> has no enum-membership check
/// today (validated downstream via <c>Enum.TryParse</c>/<c>IsDefined</c> in <c>Program.cs</c>, not by
/// MiniValidator), and <c>PaymentRequest.Amount</c>/<c>Currency</c> have no positivity/format check
/// today either. Party Review (Neo) found both were dead code -- authored, unit-tested, but never
/// wired into DI or a route -- and flagged wiring a no-op as pure ceremony with nothing to show for
/// it. Deleted rather than wired; the status/amount inline checks in <c>Program.cs</c> remain the
/// real (and correct, matching today's behavior) validation for those two DTOs. See
/// <c>agenda-buddy-02e</c> for the tracked gap this leaves (only 2 of 10 routes now validate via
/// Validot, not the full Requirement 6 migration).
/// </remarks>
public static class AppointmentExtrasRequestsSpecifications
{
    public static readonly Specification<NoteRequest> NoteSpec = s => s
        .Member(m => m.Content, m => m.Required().NotWhiteSpace());
}
