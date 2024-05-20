using System.ComponentModel.DataAnnotations;
using Library.Dtos;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Library.Entities;

public class ServiceEntity
{
    [BsonElement("_id")]
    public ObjectId Id { get; set; }
    
    [Required]
    [BsonElement("name")]
    public string Name { get; set; } = null!;
    
    [Required]
    [BsonElement("description")]
    public string Description { get; set; } = null!;
    
    [BsonElement("fee")]
    public double? Fee { get; set; } = 0;
    
    [BsonElement("unit")]
    public Unit? Unit { get; private set; }

    public ServiceEntity(){ }

    public ServiceEntity(string name, string description, double fee, Unit unit)
    {
        Name = name;
        Description = description;
        Fee = fee;
        Unit = unit;
    }
}