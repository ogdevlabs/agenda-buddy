namespace AgendaBuddy.Library.Entities;

[ExcludeFromCodeCoverage]
public class PaymentEntity
{
    public PaymentEntity() { }

    public PaymentEntity(
        string appointmentIdentifier,
        string providerEmail,
        string customerEmail,
        decimal amount,
        string currency = "usd")
    {
        AppointmentIdentifier = appointmentIdentifier;
        ProviderEmail = providerEmail;
        CustomerEmail = customerEmail;
        Amount = amount;
        Currency = currency;
    }

    [BsonElement("_id")] public ObjectId Id { get; set; }

    [Required]
    [BsonElement("appointment_identifier")]
    public string AppointmentIdentifier { get; set; } = null!;

    [Required]
    [EmailAddress]
    [BsonElement("provider_email")]
    public string ProviderEmail { get; set; } = null!;

    [Required]
    [EmailAddress]
    [BsonElement("customer_email")]
    public string CustomerEmail { get; set; } = null!;

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("amount_minor")]
    public long AmountMinor { get; set; }

    [BsonElement("provider_amount_minor")]
    public long ProviderAmountMinor { get; set; }

    [BsonElement("application_fee_minor")]
    public long ApplicationFeeMinor { get; set; }

    [BsonElement("fee_basis_points")]
    public int FeeBasisPoints { get; set; }

    [BsonElement("authorization_attempt")]
    public int AuthorizationAttempt { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "usd";

    [BsonElement("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [BsonElement("status")]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("authorized_at")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? AuthorizedAt { get; set; }

    [BsonElement("authorization_expires_at")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? AuthorizationExpiresAt { get; set; }

    [BsonElement("captured_at")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CapturedAt { get; set; }

    [BsonElement("last_external_event_at")]
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastExternalEventAt { get; set; }
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3,
    Authorized = 4,
    Cancelled = 5,
    RequiresAction = 6,
    Disputed = 7
}
