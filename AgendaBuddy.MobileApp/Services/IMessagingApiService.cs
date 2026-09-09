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

    /// <summary>
    /// Marks every message the counterpart sent in this thread as read, in one request, and answers how many
    /// changed — or <c>null</c> when the server could not be reached.
    /// </summary>
    /// <remarks>
    /// <c>long?</c> rather than <c>long</c> for the same reason <c>GetUnreadCountAsync</c> is: a caller cannot
    /// word "the server was not reached" and "there was nothing left to mark" differently if both arrive as 0.
    /// Replaces a per-message loop that issued one request — and one server-side read-then-replace — for every
    /// unread message in the thread.
    /// </remarks>
    Task<long?> MarkThreadReadAsync(string counterpartEmail, CancellationToken ct = default);
}
