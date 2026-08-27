namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
public class MessageEntity
{
    public MessageEntity() { }

    public MessageEntity(
        string senderEmail,
        string recipientEmail,
        string body)
    {
        SenderEmail = senderEmail;
        RecipientEmail = recipientEmail;
        Body = body;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    [Required]
    [EmailAddress]
    [BsonElement("sender_email")]
    public string SenderEmail { get; set; } = null!;

    [Required]
    [EmailAddress]
    [BsonElement("recipient_email")]
    public string RecipientEmail { get; set; } = null!;

    [Required]
    [BsonElement("body")]
    public string Body { get; set; } = null!;

    [BsonElement("sent_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [BsonElement("is_read")]
    public bool IsRead { get; set; } = false;

    [BsonElement("thread_id")]
    public string ThreadId { get; set; } = string.Empty;
}
