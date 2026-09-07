using AgendaBuddy.MobileApp.Models;

namespace AgendaBuddy.MobileApp.Services;

public interface IMessagingApiService
{
    /// <summary>
    /// The caller's conversations. Throws when the inbox could not be read — an empty list means an empty
    /// inbox and nothing else, so a failure cannot be drawn as "no messages yet".
    /// </summary>
    Task<List<MessageThreadStub>> GetInboxAsync(CancellationToken ct = default);

    /// <summary>One conversation, keyed on the counterpart's email. Throws when it could not be read.</summary>
    Task<List<MessageSummary>> GetThreadAsync(string counterpartEmail, CancellationToken ct = default);

    /// <summary>
    /// Sends a message. Never throws for a server refusal — a <c>403</c> (no subscription between the two
    /// parties) is a distinct outcome from an unreachable server, and the caller has to word them differently.
    /// </summary>
    Task<MessageSendResult> SendMessageAsync(string recipientEmail, string body, CancellationToken ct = default);

    Task<MessageSummary?> MarkReadAsync(string id, CancellationToken ct = default);
}
