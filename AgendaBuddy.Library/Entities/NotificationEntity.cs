namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
public class NotificationEntity
{
    public NotificationEntity() { }

    public NotificationEntity(
        string recipientEmail,
        string subject,
        string body,
        NotificationType type,
        string appointmentIdentifier)
    {
        RecipientEmail = recipientEmail;
        Subject = subject;
        Body = body;
        Type = type;
        AppointmentIdentifier = appointmentIdentifier;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    [Required]
    [BsonElement("recipient_email")]
    public string RecipientEmail { get; set; } = null!;

    [Required]
    [BsonElement("subject")]
    public string Subject { get; set; } = null!;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("type")]
    public NotificationType Type { get; set; }

    [BsonElement("appointment_identifier")]
    public string AppointmentIdentifier { get; set; } = string.Empty;

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("is_read")]
    public bool IsRead { get; set; } = false;
}

public enum NotificationType
{
    AppointmentBooked,
    AppointmentUpdated,
    AppointmentCancelled,
    AppointmentCompleted,
    PasswordResetRequested
}
