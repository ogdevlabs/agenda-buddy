using System.Net;

namespace AgendaBuddy.MobileApp.Models;

/// <summary>
/// The outcome of sending a message: the stored message, or the reason it was not sent.
/// </summary>
/// <remarks>
/// A bare <c>MessageSummary?</c> could not tell a refusal from a dropped connection, so every failure was
/// worded as "check your connection" — including the one case that has nothing to do with the network:
/// <c>POST /api/v1/messages</c> answers <c>403</c> when the two parties are not on either side of a
/// subscription. Telling somebody to check their connection when the app should not have offered the button
/// sends them to fix a network that was working.
/// </remarks>
/// <param name="Message">
/// The stored message, when the server returned one. <c>null</c> on a success too: a <c>201</c> whose body
/// did not bind still stored the message, and reporting that as a failure invites a duplicate send.
/// </param>
public sealed record MessageSendResult(bool Succeeded, MessageSummary? Message, string? ErrorMessage)
{
    /// <summary>Shown for a <c>403</c>. States the rule, because the rule is the reason.</summary>
    internal const string NotPermittedMessage =
        "You can only message people you have a subscription with.";

    /// <summary>Shown when the request never got an answer, which is the only case a connection is implicated in.</summary>
    internal const string UnreachableMessage =
        "Could not send message. Check your connection and try again.";

    /// <summary>Shown for any other refusal — an empty body, or messaging yourself.</summary>
    internal const string RejectedMessage = "Could not send message. Try again.";

    public static MessageSendResult Sent(MessageSummary? message) => new(true, message, null);

    public static MessageSendResult Unreachable() => new(false, null, UnreachableMessage);

    /// <summary>
    /// Words a refusal from its status code. <c>403</c> is the subscription rule; anything else is a
    /// rejection this client cannot narrate, so it says so rather than blaming the network.
    /// </summary>
    public static MessageSendResult Refused(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Forbidden => new(false, null, NotPermittedMessage),
        _ => new(false, null, RejectedMessage)
    };
}
