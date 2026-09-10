namespace AgendaBuddy.Library.Services;

public class PaymentService(
    IRepository<PaymentEntity> repository,
    IPaymentGateway gateway) : IPaymentService
{
    public const int ApplicationFeeBasisPoints = 1000;
    public static readonly TimeSpan MaximumAuthorizationLeadTime = TimeSpan.FromDays(4);

    public async Task<PaymentEntity> AuthorizeAsync(
        AppointmentEntity appointment, CustomerEntity customer, ProviderEntity provider)
    {
        var existing = await GetByAppointmentAsync(appointment.Identifier);
        if (existing is not null)
        {
            if (existing.Status == PaymentStatus.Authorized) return existing;
            if (existing.Status is not (PaymentStatus.Pending or PaymentStatus.Failed or PaymentStatus.RequiresAction))
                throw new InvalidOperationException("This appointment already has a payment operation.");
        }

        if (appointment.PaymentAmountMinor is null or <= 0 || string.IsNullOrWhiteSpace(appointment.PaymentCurrency))
            throw new InvalidOperationException("This appointment has no valid price snapshot.");
        if (customer.StripeCustomerId is null || customer.StripeDefaultPaymentMethodId is null)
            throw new InvalidOperationException("The customer must add a payment method before confirmation.");
        if (provider.StripeConnectedAccountId is null || !provider.StripeChargesEnabled || !provider.StripePayoutsEnabled)
            throw new InvalidOperationException("The provider must finish payout setup before confirmation.");
        if (appointment.Start > DateTime.UtcNow.Add(MaximumAuthorizationLeadTime))
            throw new InvalidOperationException(
                "Paid bookings can only be confirmed within four days of the appointment so the payment hold does not expire.");

        var applicationFee = checked((appointment.PaymentAmountMinor.Value * ApplicationFeeBasisPoints + 5000) / 10000);
        var payment = existing ?? new PaymentEntity(
                appointment.Identifier,
                appointment.EmailProvider,
                appointment.EmailCustomer,
                appointment.PaymentAmountMinor.Value / 100m,
                appointment.PaymentCurrency)
        {
            Id = ObjectId.GenerateNewId(),
            AmountMinor = appointment.PaymentAmountMinor.Value,
            ApplicationFeeMinor = applicationFee,
            ProviderAmountMinor = appointment.PaymentAmountMinor.Value - applicationFee,
            FeeBasisPoints = ApplicationFeeBasisPoints,
            CreatedAt = DateTime.UtcNow
        };
        payment.AuthorizationAttempt++;
        payment.Status = PaymentStatus.Pending;
        if (existing is null)
            await repository.InsertAsync(payment);
        else
            await repository.UpdateAsync(payment.Id.ToString(), payment);

        var authorization = await gateway.AuthorizeAsync(new PaymentAuthorizationRequest(
            payment.AmountMinor,
            payment.Currency,
            customer.StripeCustomerId,
            customer.StripeDefaultPaymentMethodId,
            provider.StripeConnectedAccountId,
            payment.ApplicationFeeMinor,
            $"Appointment {appointment.Identifier} - {appointment.ServiceName}",
            $"{appointment.Identifier}:{payment.AuthorizationAttempt}"));

        payment.StripePaymentIntentId = authorization.PaymentIntentId;
        payment.Status = authorization.Status switch
        {
            "requires_capture" => PaymentStatus.Authorized,
            "requires_action" => PaymentStatus.RequiresAction,
            _ => PaymentStatus.Failed
        };
        payment.AuthorizedAt = payment.Status == PaymentStatus.Authorized ? DateTime.UtcNow : null;
        payment.AuthorizationExpiresAt = authorization.CaptureBefore;
        await repository.UpdateAsync(payment.Id.ToString(), payment);
        return payment;
    }

    public async Task<bool> CaptureAsync(string appointmentIdentifier)
    {
        var payment = await GetByAppointmentAsync(appointmentIdentifier);
        if (payment is null) return true;
        if (payment.Status == PaymentStatus.Succeeded) return true;
        if (payment.Status != PaymentStatus.Authorized || payment.StripePaymentIntentId is null) return false;
        if (payment.AuthorizationExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
        {
            payment.Status = PaymentStatus.Cancelled;
            await repository.UpdateAsync(payment.Id.ToString(), payment);
            return false;
        }

        if (!await gateway.CaptureAsync(payment.StripePaymentIntentId, appointmentIdentifier)) return false;
        payment.Status = PaymentStatus.Succeeded;
        payment.CapturedAt = DateTime.UtcNow;
        return await repository.UpdateAsync(payment.Id.ToString(), payment);
    }

    public async Task ReconcileAsync(string paymentIntentId, string externalStatus, DateTime eventCreatedAt)
    {
        var payment = await repository.FindOneAsync(new BsonDocument("stripe_payment_intent_id", paymentIntentId));
        if (payment is null || payment.LastExternalEventAt is { } lastEvent && lastEvent >= eventCreatedAt) return;

        var status = externalStatus switch
        {
            "requires_capture" => PaymentStatus.Authorized,
            "succeeded" or "processing" => PaymentStatus.Succeeded,
            "canceled" => PaymentStatus.Cancelled,
            "refunded" => PaymentStatus.Refunded,
            "requires_action" => PaymentStatus.RequiresAction,
            "requires_payment_method" => PaymentStatus.Failed,
            "disputed" => PaymentStatus.Disputed,
            _ => payment.Status
        };
        payment.LastExternalEventAt = eventCreatedAt;
        if (status == payment.Status)
        {
            await repository.UpdateAsync(payment.Id.ToString(), payment);
            return;
        }

        payment.Status = status;
        if (status == PaymentStatus.Authorized) payment.AuthorizedAt ??= DateTime.UtcNow;
        if (status == PaymentStatus.Succeeded) payment.CapturedAt ??= DateTime.UtcNow;
        await repository.UpdateAsync(payment.Id.ToString(), payment);
    }

    public async Task<PaymentEntity?> GetByAppointmentAsync(string appointmentIdentifier)
    {
        var filter = new BsonDocument("appointment_identifier", appointmentIdentifier);
        return await repository.FindOneAsync(filter);
    }

    public async Task<bool> ReleaseOrRefundAsync(string appointmentIdentifier)
    {
        var payment = await GetByAppointmentAsync(appointmentIdentifier);
        if (payment is null || payment.Status is PaymentStatus.Cancelled or PaymentStatus.Refunded or PaymentStatus.Failed)
            return true;

        if (payment.StripePaymentIntentId is null)
            return false;

        var released = payment.Status == PaymentStatus.Succeeded
            ? await gateway.RefundPaymentIntentAsync(payment.StripePaymentIntentId)
            : await gateway.CancelPaymentIntentAsync(payment.StripePaymentIntentId);

        if (!released) return false;

        payment.Status = payment.Status == PaymentStatus.Succeeded
            ? PaymentStatus.Refunded
            : PaymentStatus.Cancelled;
        return await repository.UpdateAsync(payment.Id.ToString(), payment);
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
