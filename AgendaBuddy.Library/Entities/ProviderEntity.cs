#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
public class ProviderEntity
{
    public ProviderEntity(string firstName, string lastName, string email, string? kafkaTopic,
        List<ServiceEntity> serviceEntities, List<AppointmentEntity> appointmentEntities,
        List<string> subscribedCustomerCollection)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        KafkaTopic = kafkaTopic;
        ServiceEntities = serviceEntities;
        AppointmentEntities = appointmentEntities;
        SubscribedCustomerCollection = subscribedCustomerCollection;
    }

    public ProviderEntity()
    {
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    [Required][BsonElement("first_name")] public string FirstName { get; set; }

    [Required][BsonElement("last_name")] public string LastName { get; set; }

    [Required]
    [EmailAddress]
    [BsonElement("email")]
    public string Email { get; set; }

    [BsonElement("kafka_topic")] public string? KafkaTopic { get; set; }

    [BsonElement("services")] public List<ServiceEntity> ServiceEntities { get; set; } = [];

    [BsonElement("appointments")] public List<AppointmentEntity> AppointmentEntities { get; set; } = [];

    [BsonElement("subscribed_customer_collection")] public List<string> SubscribedCustomerCollection { get; set; } = [];

    [BsonElement("is_active")] public bool IsActive { get; set; } = true;
}
