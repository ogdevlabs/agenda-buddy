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

    [BsonElement("currency")]
    public string Currency { get; set; } = "usd";

    [BsonElement("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [BsonElement("status")]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded
}
