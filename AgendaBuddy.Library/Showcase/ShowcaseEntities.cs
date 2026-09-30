namespace AgendaBuddy.Library.Showcase;

/// <summary>
/// A provider's showcase. Kept out of <see cref="ProviderEntity"/> because the provider profile <c>PUT</c> replaces
/// the whole document, and a photo stored there would be erased by the next profile save (ADR-071).
/// </summary>
[BsonIgnoreExtraElements]
public class ProviderShowcaseEntity
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("provider_id")] public ObjectId ProviderId { get; set; }

    [BsonElement("provider_email")] public string ProviderEmail { get; set; } = string.Empty;

    [BsonElement("public_code")]
    [BsonIgnoreIfNull]
    public string? PublicCode { get; set; }

    [BsonElement("tagline")] public string? Tagline { get; set; }

    [BsonElement("about")] public string? About { get; set; }

    [BsonElement("photo_hash")] public string? PhotoHash { get; set; }

    [BsonElement("logo_hash")] public string? LogoHash { get; set; }

    /// <summary>Display order; element 0 is the cover.</summary>
    [BsonElement("portfolio")] public List<PortfolioItem> Portfolio { get; set; } = [];

    /// <summary>Operator takedown: the showcase and its media answer 404 to everyone but the owner.</summary>
    [BsonElement("hidden_by_operator")] public bool? HiddenByOperator { get; set; }

    [BsonElement("updated_at")] public DateTime UpdatedAt { get; set; }

    [BsonElement("portfolio_changed_at")] public DateTime? PortfolioChangedAt { get; set; }
}

[BsonIgnoreExtraElements]
public class PortfolioItem
{
    [BsonElement("hash")] public string Hash { get; set; } = string.Empty;

    [BsonElement("caption")] public string? Caption { get; set; }

    [BsonElement("service_id")] public ObjectId? ServiceId { get; set; }

    [BsonElement("width")] public int Width { get; set; }

    [BsonElement("height")] public int Height { get; set; }

    [BsonElement("added_at")] public DateTime AddedAt { get; set; }
}

/// <summary>One stored image, owned by one provider. Its presence is what authorises attaching the hash.</summary>
[BsonIgnoreExtraElements]
public class MediaRefEntity
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("provider_id")] public ObjectId ProviderId { get; set; }

    [BsonElement("hash")] public string Hash { get; set; } = string.Empty;

    [BsonElement("attached")] public bool Attached { get; set; }

    [BsonElement("width")] public int Width { get; set; }

    [BsonElement("height")] public int Height { get; set; }

    [BsonElement("bytes")] public long Bytes { get; set; }

    [BsonElement("created_at")] public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the image last stopped being referenced. The sweep keeps it for 24 hours after this, not after
    /// <see cref="CreatedAt"/>, so undoing a removal of an old image still finds its bytes.
    /// </summary>
    [BsonElement("detached_at")] public DateTime? DetachedAt { get; set; }
}

/// <summary>One row per (provider, customer) relationship, never one per visit.</summary>
[BsonIgnoreExtraElements]
public class ShowcaseVisitEntity
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("provider_id")] public ObjectId ProviderId { get; set; }

    [BsonElement("customer_email")] public string CustomerEmail { get; set; } = string.Empty;

    [BsonElement("first_source")] public string FirstSource { get; set; } = ShowcaseSources.Directory;

    [BsonElement("first_seen_at")] public DateTime FirstSeenAt { get; set; }

    [BsonElement("last_seen_at")] public DateTime LastSeenAt { get; set; }

    [BsonElement("visit_count")] public int VisitCount { get; set; }

    [BsonElement("source_counts")] public Dictionary<string, int> SourceCounts { get; set; } = [];
}

/// <summary>Anonymous scan counts. Holds no IP address, browser identifier, timestamp or visitor identifier.</summary>
[BsonIgnoreExtraElements]
public class GoCounterEntity
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("code")] public string Code { get; set; } = string.Empty;

    [BsonElement("provider_id")] public ObjectId ProviderId { get; set; }

    [BsonElement("ios")] public long Ios { get; set; }

    [BsonElement("android")] public long Android { get; set; }

    [BsonElement("other")] public long Other { get; set; }
}

[BsonIgnoreExtraElements]
public class ShowcaseReportEntity
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("provider_id")] public ObjectId ProviderId { get; set; }

    [BsonElement("reporter_email")] public string ReporterEmail { get; set; } = string.Empty;

    [BsonElement("reason")] public string Reason { get; set; } = string.Empty;

    [BsonElement("detail")] public string? Detail { get; set; }

    [BsonElement("portfolio_hash")] public string? PortfolioHash { get; set; }

    [BsonElement("created_at")] public DateTime CreatedAt { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "open";
}

[BsonIgnoreExtraElements]
public class ShowcaseBlockEntity
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [BsonElement("customer_email")] public string CustomerEmail { get; set; } = string.Empty;

    [BsonElement("provider_id")] public ObjectId ProviderId { get; set; }

    [BsonElement("created_at")] public DateTime CreatedAt { get; set; }
}

public static class ShowcaseSources
{
    public const string Scan = "scan";
    public const string Code = "code";
    public const string Directory = "directory";
    public const string Appointment = "appointment";
    public const string Message = "message";
    public const string Booking = "booking";

    public static readonly IReadOnlyList<string> All = [Scan, Code, Directory, Appointment, Message, Booking];

    /// <summary>An unknown or missing source is recorded as <see cref="Directory"/> rather than rejected.</summary>
    public static string Normalise(string? source)
    {
        var trimmed = source?.Trim().ToLowerInvariant();
        return trimmed is not null && All.Contains(trimmed) ? trimmed : Directory;
    }
}

public static class ShowcaseReportReasons
{
    public static readonly IReadOnlyList<string> All = ["inappropriate", "not_their_work", "spam", "other"];
}
