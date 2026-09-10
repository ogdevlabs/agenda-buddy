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


    /// <summary>
    /// Which avatar this account is drawn with. Assigned at creation from <see cref="AvatarCatalog"/>.
    /// </summary>
    /// <remarks>
    /// Empty on every account that predates avatar assignment, and on any whose profile creation failed —
    /// <c>AvatarCatalog.Resolve</c> falls back to a stable choice from the email, so no migration is needed and
    /// nobody renders as a blank circle.
    /// </remarks>
    [BsonElement("avatar_id")]
    public string AvatarId { get; set; } = string.Empty;

    /// <summary>
    /// When this account last confirmed the Terms and Conditions. <c>null</c> means never.
    /// </summary>
    /// <remarks>
    /// A timestamp rather than a boolean: "they agreed" without a date is not a record of anything, and the two
    /// documents are revised independently, so they get a field each. It records the <b>latest</b> confirmation,
    /// not the first — what matters is consent to the text currently published, and re-confirming is the act that
    /// establishes it. Additive and nullable, so every account that predates consent capture reads back as
    /// not-yet-accepted rather than failing to deserialise.
    /// </remarks>
    [BsonElement("terms_accepted_at")]
    [BsonIgnoreIfNull]
    public DateTime? TermsAcceptedAt { get; set; }

    /// <summary>When this account last confirmed the Privacy Policy. See <see cref="TermsAcceptedAt"/>.</summary>
    [BsonElement("privacy_accepted_at")]
    [BsonIgnoreIfNull]
    public DateTime? PrivacyAcceptedAt { get; set; }

    [BsonElement("subscribed_provider_collection")]
    public List<string>? SubscribedProviderCollection { get; set; } = [];

    [BsonElement("appointment_identifier_collection")]
    public List<string>? AppointmentCollection { get; set; } = [];

    [BsonElement("stripe_customer_id")]
    [BsonIgnoreIfNull]
    public string? StripeCustomerId { get; set; }

    [BsonElement("stripe_default_payment_method_id")]
    [BsonIgnoreIfNull]
    public string? StripeDefaultPaymentMethodId { get; set; }

    [BsonElement("payment_method_label")]
    [BsonIgnoreIfNull]
    public string? PaymentMethodLabel { get; set; }

    [BsonElement("payment_method_type")]
    [BsonIgnoreIfNull]
    public string? PaymentMethodType { get; set; }

    [BsonElement("payment_method_brand")]
    [BsonIgnoreIfNull]
    public string? PaymentMethodBrand { get; set; }

    [BsonElement("payment_method_last4")]
    [BsonIgnoreIfNull]
    public string? PaymentMethodLast4 { get; set; }
}
