namespace AgendaBuddy.Library.Entities;

/// <summary>
/// One account's subscribable calendar feed. At most one per owner: enabling again replaces it, which is what
/// revokes a leaked link.
/// </summary>
/// <remarks>
/// Holds the SHA-256 of the URL token, never the token itself (ADR-074) — a database read must not hand out a live
/// subscription URL. The token exists in plain form only in the response that created it and on the owner's device.
/// </remarks>
[ExcludeFromCodeCoverage]
[BsonIgnoreExtraElements]
public class CalendarFeedEntity
{
    public CalendarFeedEntity()
    {
    }

    [SetsRequiredMembers]
    public CalendarFeedEntity(string ownerEmail, string tokenHash, string language)
    {
        OwnerEmail = ownerEmail;
        TokenHash = tokenHash;
        Language = language;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("identifier")]
    public string Identifier { get; init; } = Guid.NewGuid().ToString();

    [BsonElement("owner_email")]
    [EmailAddress]
    public required string OwnerEmail { get; set; } = string.Empty;

    [BsonElement("token_hash")]
    public required string TokenHash { get; set; } = string.Empty;

    /// <summary>The language event titles are written in, fixed when the feed is enabled.</summary>
    [BsonElement("language")]
    public string Language { get; set; } = "en";

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
