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
}

public enum FeeType
{
    Hourly,
    Fixed,
    Subscription
}
