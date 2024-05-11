using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Library.Entities;

public class Provider
{
    [BsonElement("_id")]
    public Object Id { get; set; }
    [BsonElement("first_name")]
    public required string FirstName { get; set; }
    [BsonElement("last_name")]
    public required string LastName { get; set; }
    [EmailAddress]
    [BsonElement("email")]
    public required string Email { get; set; }
    [BsonElement("kafka_topic")]
    public string? KafkaTopic { get; set; }
    
    public Provider(string firstName, string lastName, string email, string? kafkaTopic)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        KafkaTopic = kafkaTopic;
    }
}