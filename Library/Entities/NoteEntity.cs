namespace Library.Entities;

[ExcludeFromCodeCoverage]
public class NoteEntity
{
    public NoteEntity() { }

    public NoteEntity(string providerEmail, string appointmentIdentifier, string content)
    {
        ProviderEmail = providerEmail;
        AppointmentIdentifier = appointmentIdentifier;
        Content = content;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    [Required]
    [EmailAddress]
    [BsonElement("provider_email")]
    public string ProviderEmail { get; set; } = null!;

    [Required]
    [BsonElement("appointment_identifier")]
    public string AppointmentIdentifier { get; set; } = null!;

    [Required]
    [BsonElement("content")]
    public string Content { get; set; } = null!;

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updated_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
