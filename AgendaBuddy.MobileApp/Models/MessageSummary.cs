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

    /// <summary>
    /// When the message was sent, on THIS DEVICE'S clock.
    /// </summary>
    /// <remarks>
    /// Stored and returned as UTC, but every consumer treats it as wall-clock: the thread formats it
    /// <c>{0:h:mm tt}</c> and <see cref="MessageThreadStub.TimeAgo"/> subtracts it from
    /// <see cref="DateTime.Now"/>. Left in UTC, a message sent seconds ago at UTC-6 displayed as "1:23 AM"
    /// and read as "-355m ago". Converted once on the way in — the same normalisation
    /// <c>CalendarApiService</c> applies to an appointment's ScheduledAt, and for the same reason. Nothing
    /// sends this value back, so it cannot round-trip a shifted instant.
    /// </remarks>
    public DateTime SentAt
    {
        get => _sentAt;
        set => _sentAt = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
    }

    private DateTime _sentAt;

    public bool IsRead { get; set; }

    /// <summary>
    /// True when the signed-in user sent this. Set by the thread ViewModel, which knows who the counterpart
    /// is — every bubble was previously drawn identically (accent fill, right-aligned), so a conversation
    /// gave no indication of who had said what.
    /// </summary>
    /// <remarks>
    /// Alignment is derived from this in XAML rather than exposed here as a <c>LayoutOptions</c>: this model
    /// also compiles into the net10.0 slice the tests run against, which has no MAUI types.
    /// </remarks>
    public bool IsMine { get; set; }
}
