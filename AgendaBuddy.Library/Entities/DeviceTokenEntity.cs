namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
public class DeviceTokenEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("user_email")]
    [Required]
    [EmailAddress]
    public string UserEmail { get; set; } = string.Empty;

    [BsonElement("token")]
    [Required]
    public string Token { get; set; } = string.Empty;

    [BsonElement("platform")]
    [Required]
    public string Platform { get; set; } = string.Empty;

    [BsonElement("registered_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updated_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
