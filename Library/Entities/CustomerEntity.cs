namespace Library.Entities;

[ExcludeFromCodeCoverage]
public class CustomerEntity
{
    public CustomerEntity(ObjectId id, string? firstName, string? lastName, string? email,
        string? kafkaTopic, List<string>? subscribedEmailProviderCollection, List<string> appointmentIdentifierCollection)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        KafkaTopic = kafkaTopic;
        SubscribedEmailProviderCollection = subscribedEmailProviderCollection;
        AppointmentIdentifierCollection = appointmentIdentifierCollection;
    }

    public CustomerEntity()
    {
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }
    [Required] [BsonElement("first_name")] public string? FirstName { get; set; }
    [Required] [BsonElement("last_name")] public string? LastName { get; set; }

    [BsonElement("email")]
    [EmailAddress]
    [Required]
    public string? Email { get; set; }

    [BsonElement("kafka_topic")] public string? KafkaTopic { get; set; }

    [BsonElement("subscribed_email_provider_collection")]
    public List<string>? SubscribedEmailProviderCollection { get; set; } = [];

    [BsonElement("appointment_identifier_collection")]
    public List<string>? AppointmentIdentifierCollection { get; set; } = [];
}