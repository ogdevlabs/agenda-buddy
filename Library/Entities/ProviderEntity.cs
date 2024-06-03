#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Library.Entities;

public class ProviderEntity
{
    [BsonElement("_id")] public ObjectId Id { get; set; }

    [Required] [BsonElement("first_name")] public string FirstName { get; set; }

    [Required] [BsonElement("last_name")] public string LastName { get; set; }

    [Required]
    [EmailAddress]
    [BsonElement("email")]
    public string Email { get; set; }

    [BsonElement("kafka_topic")] public string? KafkaTopic { get; set; }

    [BsonElement("services")] public List<ServiceEntity> ServiceEntities { get; set; } = [];

    public ProviderEntity(string firstName, string lastName, string email, string? kafkaTopic,
        List<ServiceEntity> serviceEntities)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        KafkaTopic = kafkaTopic;
        ServiceEntities = serviceEntities;
    }

    public ProviderEntity()
    {
    }
}