using Library.Dtos;
using MongoDB.Bson.Serialization.Attributes;

namespace Library.Entities;

public class Service
{
    [BsonElement("_id")]
    public Object Id { get; set; }
    [BsonElement("name")]
    public required string Name { get; set; } = null;
    [BsonElement("description")]
    public string? Description { get; set; } = null;
    [BsonElement("fee")]
    public double? Fee { get; set; } = 0;
    [BsonElement("unit")]
    public Unit? Unit { get; private set; }

    public Service(string name, string description, double fee, Unit unit)
    {
        Name = name;
        Description = description;
        Fee = fee;
        Unit = unit;
    }
}