namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
public class ServiceEntity
{
    public ServiceEntity()
    {
    }

    public ServiceEntity(string name, string description, decimal fee)
    {
        Name = name;
        Description = description;
        Fee = fee;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    [Required][BsonElement("name")] public string Name { get; set; } = null!;

    [Required]
    [BsonElement("description")]
    public string Description { get; set; } = null!;

    [BsonElement("fee")] public decimal? Fee { get; set; } = 0;

    [BsonElement("feeType")] public FeeType FeeType { get; set; }

    [BsonElement("isActive")] public bool IsActive { get; set; } = true;

    /// <summary>The session length this service books for, in minutes. Additive field (2026-08-28) — a
    /// missing/null value on an older stored document reads back as null, not zero.</summary>
    [BsonElement("duration_minutes")] public int? DurationMinutes { get; set; }

    /// <summary>The provider's own Profession (<see cref="ProviderEntity.Professions"/>) this service is
    /// offered under. Additive field (2026-08-28) — null on services created before this existed. A new
    /// service must name one of the provider's current professions (enforced in
    /// AddServicesToProviderCommandHandler); existing services keep whatever they had, even null.</summary>
    [BsonElement("profession_name")] public string? ProfessionName { get; set; }
}

public enum FeeType
{
    Hourly,
    Fixed,
    Subscription
}
