namespace Booking.Core.Commands;

// F-019-T05 / CONSTITUTION §3. See GetAppointmentNotesQueryHandler's remarks on the audit gap this
// refactor exposed. The Conflict throw path stays unaudited, matching every other
// exception-propagation path in this project's handlers.
public class PayForAppointmentCommandHandler(IPaymentService payments, IEventStore eventStore)
    : IRequestHandler<PayForAppointmentCommand, Result<PaymentEntity>>
{
    public async Task<Result<PaymentEntity>> Handle(PayForAppointmentCommand request, CancellationToken cancellationToken)
    {
        GuardClause.ArgumentIsNotNull(request, nameof(request));

        // Threat T-205's Conflict case: a second charge for the same appointment. Thrown, not
        // Result.Fail'd -- matching ChangeAppointmentStatusCommandHandler's InvalidOperationException
        // -> 409 precedent, since a generic Result.Fail can't distinguish "conflict" from an
        // ordinary failure in Booking.Api's status-code mapping.
        if (await payments.GetByAppointmentAsync(request.Identifier) is not null)
            throw new InvalidOperationException("This appointment has already been paid.");

        var charged = await payments.ChargeAsync(new PaymentEntity
        {
            AppointmentIdentifier = request.Identifier,
            ProviderEmail = request.ProviderEmail,
            CustomerEmail = request.CustomerEmail,
            Amount = request.Amount,
            Currency = request.Currency
        });

        await eventStore.SaveAsync(new Event
        {
            Id = ObjectId.GenerateNewId(),
            TimeStamp = DateTime.UtcNow,
            Status = "Success",
            Type = nameof(PayForAppointmentCommand),
            Data = JsonSerializer.Serialize(charged)
        });

        return Result.Ok(charged);
    }
}
