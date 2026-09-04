#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
// Documents written before the Kafka topic-per-provider mechanism was removed still carry a
// kafka_topic element. There is no property for it any more, and the driver throws on an unmapped
// element unless told otherwise, so ignoring extras is what keeps those documents readable.
[BsonIgnoreExtraElements]
public class ProviderEntity
{
    public ProviderEntity(string firstName, string lastName, string email,
        List<ServiceEntity> serviceEntities, List<AppointmentEntity> appointmentEntities,
        List<string> subscribedCustomerCollection)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
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

    /// <summary>
    /// Contact number shown to customers who have booked. Optional, for the same reason as
    /// <see cref="CustomerEntity.PhoneNumber"/>.
    /// </summary>
    [Phone]
    [BsonElement("phone_number")]
    [BsonIgnoreIfNull]
    public string? PhoneNumber { get; set; }


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

    /// <summary>
    /// First bookable hour of the provider's working day, on their own clock (see <see cref="TimeZoneId"/>).
    /// </summary>
    /// <remarks>
    /// Additive and nullable: a document written before this field existed reads back as <c>null</c>, which
    /// <see cref="Tools.AvailabilityCalculator"/> resolves to its own default. Whole hours only — the slot
    /// grid steps by the hour, so a half-past start has nowhere to land.
    /// </remarks>
    [BsonElement("work_day_start_hour")]
    [BsonIgnoreIfNull]
    [Range(0, 23, ErrorMessage = "Work day start hour must be between 0 and 23.")]
    public int? WorkDayStartHour { get; set; }

    /// <summary>
    /// The hour by which a session must have ENDED, on the provider's own clock — an exclusive bound, so 17
    /// means the last session finishes at 17:00.
    /// </summary>
    [BsonElement("work_day_end_hour")]
    [BsonIgnoreIfNull]
    [Range(1, 24, ErrorMessage = "Work day end hour must be between 1 and 24.")]
    public int? WorkDayEndHour { get; set; }
}
