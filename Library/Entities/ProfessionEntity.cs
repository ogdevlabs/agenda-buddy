namespace Library.Entities;

public sealed class ProfessionEntity
{
    public ProfessionEntity(ObjectId id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public ProfessionEntity()
    {
        
    }
    [BsonElement("_id")] public ObjectId Id { get; set; }
    [BsonElement("name")] public required string Name { get; set; }
    [BsonElement("description")] public required string Description { get; set; }
}