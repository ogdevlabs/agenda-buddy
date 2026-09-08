using System.Net;

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
public sealed record AppointmentActionResult(bool Succeeded, string? ErrorMessage)
{
    /// <summary>Shown when the request never got an answer — the only case a connection is implicated in.</summary>
    internal const string UnreachableMessage = "Could not reach the server. Check your connection and try again.";

    /// <summary>Shown for a <c>403</c>: the action was not this caller's to take.</summary>
    internal const string NotPermittedMessage = "You do not have permission to do that.";

    /// <summary>Shown for a <c>404</c>. The appointment is gone, so retrying will not help.</summary>
    internal const string NotFoundMessage = "This appointment is no longer available.";

    /// <summary>Shown for a refusal the server did not narrate.</summary>
    internal const string RejectedMessage = "That could not be done. Try again.";

    public static AppointmentActionResult Done() => new(true, null);

    public static AppointmentActionResult Unreachable() => new(false, UnreachableMessage);

    /// <summary>
    /// Words a refusal, preferring the server's own explanation.
    /// </summary>
    /// <remarks>
    /// A <c>409</c> is where every state-conflict refusal on these routes arrives — too late to cancel, already
    /// answered, already completed — and the server's message is the only place the specifics exist. Falling back
    /// to a generic string for a 409 would throw away the one thing the reader needs.
    /// </remarks>
    public static AppointmentActionResult Refused(HttpStatusCode status, string? serverMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(serverMessage)) return new(false, serverMessage.Trim());

        return status switch
        {
            HttpStatusCode.Forbidden => new(false, NotPermittedMessage),
            HttpStatusCode.NotFound => new(false, NotFoundMessage),
            _ => new(false, RejectedMessage)
        };
    }
}
