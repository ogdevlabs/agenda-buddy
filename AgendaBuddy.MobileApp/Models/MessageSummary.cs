namespace AgendaBuddy.MobileApp.Models;

public class MessageSummary
{
    public string Id { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;

    // Not surfaced by MessageThreadPage's own UI, but needed to derive an inbox thread's "other party"
    // and unread count client-side — GetInbox (GET /api/v1/messages) returns a flat list of individual
    // MessageEntity objects, not thread summaries; see MessagingApiService.GetInboxAsync.
    public string RecipientEmail { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}
