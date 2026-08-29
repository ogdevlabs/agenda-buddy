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

    /// <summary>Profession catalog names (see ProfessionEntity) this provider currently practices under.
    /// Additive field (2026-08-28) — a missing value on an older stored document reads back as an empty list.</summary>
    [BsonElement("professions")] public List<string> Professions { get; set; } = [];

    /// <summary>
    /// The provider's own timezone, as an IANA id (e.g. <c>America/Mexico_City</c>) — the zone their
    /// working hours are expressed in.
    /// </summary>
    /// <remarks>
    /// Additive (2026-08-29) and nullable, so existing providers keep working; <c>null</c> is read as UTC,
    /// which is exactly the behaviour they had before this field existed. Without it, availability
    /// generated 09:00–19:00 in UTC for everyone, so a provider at UTC-6 was offered 03:00–13:00 local —
    /// slots in the middle of the night. IANA rather than a Windows id because .NET resolves IANA ids on
    /// every platform this runs on, and the mobile client reports the device zone in that form.
    /// </remarks>
    [BsonElement("time_zone_id")]
    [BsonIgnoreIfNull]
    public string? TimeZoneId { get; set; }
}
