namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
// Documents written before the Kafka topic-per-customer mechanism was removed still carry a
// kafka_topic element. There is no property for it any more, and the driver throws on an unmapped
// element unless told otherwise, so ignoring extras is what keeps those documents readable.
[BsonIgnoreExtraElements]
public class CustomerEntity
{
    public CustomerEntity(ObjectId id, string? firstName, string? lastName, string? email,
        List<string>? subscribedProviderCollection, List<string> appointmentCollection)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        SubscribedProviderCollection = subscribedProviderCollection;
        AppointmentCollection = appointmentCollection;
    }

    public CustomerEntity()
    {
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }
    [Required][BsonElement("first_name")] public string? FirstName { get; set; }
    [Required][BsonElement("last_name")] public string? LastName { get; set; }

    [BsonElement("email")]
    [EmailAddress]
    [Required]
    public string? Email { get; set; }

    /// <summary>
    /// Contact number. Optional — an account is usable without one — but it is the only fallback channel a
    /// provider has when a session is about to be missed, so registration asks for it.
    /// </summary>
    [Phone]
    [BsonElement("phone_number")]
    [BsonIgnoreIfNull]
    public string? PhoneNumber { get; set; }


    [BsonElement("subscribed_provider_collection")]
    public List<string>? SubscribedProviderCollection { get; set; } = [];

    [BsonElement("appointment_identifier_collection")]
    public List<string>? AppointmentCollection { get; set; } = [];
}
