namespace AgendaBuddy.Booking.Core.Queries;

// F-019-T05 / CONSTITUTION §3. See GetAppointmentNotesQueryHandler's remarks on the audit gap this
// refactor exposed.
public class GetAppointmentPaymentQueryHandler(IPaymentService payments, IEventStore eventStore)
    : IRequestHandler<GetAppointmentPaymentQuery, Result<PaymentEntity>>
{
    public async Task<Result<PaymentEntity>> Handle(GetAppointmentPaymentQuery request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        var payment = await payments.GetByAppointmentAsync(request.Identifier);
        if (payment is null)
        {
            await eventStore.SaveAsync(QueryAudit.Failure(nameof(GetAppointmentPaymentQuery)));
            // 404-shaped in AgendaBuddy.Booking.Api. Safe here: by the time this query is reached, the caller has
            // already proven they are a participant in the appointment (AgendaBuddy.Booking.Api's ownership check).
            return Result.Fail<PaymentEntity>("No payment found for this appointment.");
        }

        await eventStore.SaveAsync(QueryAudit.Success(nameof(GetAppointmentPaymentQuery), 1));
        return Result.Ok(payment);
    }
}
