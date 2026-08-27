namespace Booking.Requests;

/// <summary>
/// The target state for a status transition. A string rather than the enum so an unrecognised value answers
/// <b>400</b> with a usable message instead of being model-bound to <c>Requested</c> (the enum's zero value)
/// and silently attempting a transition nobody asked for.
/// </summary>
public record AppointmentStatusRequest(string Status);

/// <summary>What the status route returns: the identifier and the state it is now in.</summary>
public record AppointmentStatusResponse(string Identifier, string Status);

/// <summary>
/// A session note's content, and nothing else.
/// </summary>
/// <remarks>
/// ⚠️ <b>It deliberately has no <c>providerEmail</c> and no <c>appointmentIdentifier</c>.</b> Both are
/// determined by the server — the provider from the caller's <c>sub</c> claim, the appointment from the path.
/// Leaving them off the request type is the cheapest possible guarantee that no handler can be refactored
/// into trusting a caller for either, which is threat T-201: <c>NoteService</c> takes a
/// <c>providerEmail</c> parameter, and a route that passed a client-supplied one through would hand every
/// provider's notes to any authenticated caller.
/// </remarks>
public record NoteRequest(string Content);

/// <summary>
/// An amount and an optional currency.
/// </summary>
/// <remarks>
/// <para>
/// Also deliberately without participant emails: both come from the stored appointment (threat T-205), so a
/// caller cannot record a payment against someone else.
/// </para>
/// <para>
/// ⚠️ <b><c>Amount</c> is client-asserted and cannot be validated</b> — an appointment does not record which
/// service it was booked for, so there is no price to check it against. Accepted residual risk, documented in
/// threat T-205 and `api-contracts.md` §2. It matters little while the default gateway records rather than
/// charges; it matters a great deal to whoever first configures a real Stripe key.
/// </para>
/// </remarks>
public record PaymentRequest(decimal Amount, string? Currency);
