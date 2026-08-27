namespace AgendaBuddy.Library.Services;

public class PaymentService(
    IRepository<PaymentEntity> repository,
    IPaymentGateway gateway) : IPaymentService
{
    public async Task<PaymentEntity> ChargeAsync(PaymentEntity payment)
    {
        payment.Id = ObjectId.GenerateNewId();
        payment.CreatedAt = DateTime.UtcNow;
        payment.Status = PaymentStatus.Pending;

        var description = $"Appointment {payment.AppointmentIdentifier} - {payment.ProviderEmail}";
        var intentId = await gateway.CreatePaymentIntentAsync(payment.Amount, payment.Currency, description);
        payment.StripePaymentIntentId = intentId;

        var succeeded = await gateway.ConfirmPaymentIntentAsync(intentId);
        payment.Status = succeeded ? PaymentStatus.Succeeded : PaymentStatus.Failed;

        await repository.InsertAsync(payment);
        return payment;
    }

    public async Task<PaymentEntity?> GetByAppointmentAsync(string appointmentIdentifier)
    {
        var filter = new BsonDocument("appointment_identifier", appointmentIdentifier);
        return await repository.FindOneAsync(filter);
    }

    public async Task<PaymentEntity> RefundAsync(string appointmentIdentifier)
    {
        var filter = new BsonDocument("appointment_identifier", appointmentIdentifier);
        var payment = await repository.FindOneAsync(filter)
            ?? throw new KeyNotFoundException($"No payment found for appointment {appointmentIdentifier}.");

        if (payment.Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException("Only succeeded payments can be refunded.");

        if (payment.StripePaymentIntentId is null)
            throw new InvalidOperationException("Payment has no associated Stripe intent.");

        var refunded = await gateway.RefundPaymentIntentAsync(payment.StripePaymentIntentId);
        payment.Status = refunded ? PaymentStatus.Refunded : PaymentStatus.Failed;
        await repository.UpdateAsync(payment.Id.ToString(), payment);
        return payment;
    }
}
