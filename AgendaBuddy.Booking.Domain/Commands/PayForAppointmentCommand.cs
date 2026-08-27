namespace AgendaBuddy.Booking.Domain.Commands;

/// <summary>
/// Threat T-205: both participant emails are supplied by AgendaBuddy.Booking.Api from the STORED appointment,
/// never from the caller's request body, so a caller cannot record a payment against someone else.
/// </summary>
[ExcludeFromCodeCoverage]
public class PayForAppointmentCommand : IRequest<Result<PaymentEntity>>
{
    public required string Identifier { get; set; }
    public required string ProviderEmail { get; set; }
    public required string CustomerEmail { get; set; }
    public required decimal Amount { get; set; }
    public required string Currency { get; set; }
}
