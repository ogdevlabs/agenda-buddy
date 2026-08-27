namespace Booking.Domain.Queries;

[ExcludeFromCodeCoverage]
public class GetAppointmentPaymentQuery : IRequest<Result<PaymentEntity>>
{
    public required string Identifier { get; set; }
}
