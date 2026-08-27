namespace AgendaBuddy.Library.Entities;

public sealed class ProfessionEntity
{
    public ProfessionEntity(ObjectId id, string name)
    {
        Id = id;
        Name = name;
    }

    public ProfessionEntity()
    {

    }
    [BsonElement("_id")] public ObjectId Id { get; set; }
    [BsonElement("name")] public required string Name { get; set; }
}
