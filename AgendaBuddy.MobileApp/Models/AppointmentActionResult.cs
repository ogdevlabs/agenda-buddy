namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// The outcome of acting on an appointment — cancel, reschedule, request, answer.
/// </summary>
/// <remarks>
/// <para>
/// A bare <c>bool</c> cannot tell a REFUSAL from a dropped connection, so every failure ended up worded as
/// "check your connection" — including the ones that have nothing to do with the network. That is the same defect
/// that made a messaging <c>403</c> read as a connectivity problem, and these routes have more refusals than
/// messaging does: cancelling inside the notice period, approving a proposal for a slot somebody else took,
/// answering a request that was withdrawn.
/// </para>
/// <para>
/// <b>The server's own message is carried through when it sends one.</b> These refusals are specific and worth
/// reading — "cancellations close 24 hours before a session, this one closed on Tuesday at 3:00 PM" cannot be
/// reconstructed on the client, because only the server holds the authoritative clock. Where the server says
/// nothing, this supplies wording rather than showing an empty banner.
/// </para>
/// </remarks>
/// <param name="Succeeded">Whether the appointment actually changed.</param>
/// <param name="ErrorMessage">Why not, worded for the reader. <c>null</c> on success.</param>
public sealed record AppointmentActionResult(bool Succeeded, MobileError? Error)
{
    public string? ErrorMessage => Error?.Message;

    public static AppointmentActionResult Done() => new(true, null);

    public static AppointmentActionResult Unreachable(
        MobileOperation operation = MobileOperation.AppointmentAction, string? diagnosticDetail = null) =>
        new(false, MobileError.Unavailable(operation, diagnosticDetail));

    /// <summary>
    /// Words a refusal, preferring the server's own explanation.
    /// </summary>
    /// <remarks>
    /// A <c>409</c> is where every state-conflict refusal on these routes arrives — too late to cancel, already
    /// answered, already completed — and the server's message is the only place the specifics exist. Falling back
    /// to a generic string for a 409 would throw away the one thing the reader needs.
    /// </remarks>
    public static AppointmentActionResult Refused(
        MobileOperation operation, System.Net.HttpStatusCode status, string? diagnosticDetail = null) =>
        new(false, MobileError.FromStatus(operation, status, diagnosticDetail));
}
