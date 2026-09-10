using AgendaBuddy.Booking.Api.Payments;
using Stripe;
using Stripe.Checkout;

namespace AgendaBuddy.Booking.Api.Modules;

public sealed class PaymentWebhookModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/payments/stripe/webhook", HandleAsync)
            .WithName("HandleStripeWebhook")
            .WithTags("Payments")
            .ExcludeFromDescription();

        app.MapGet("api/v1/payments/customer/return", ([FromQuery(Name = "session_id")] string sessionId) =>
                Results.Redirect($"agendame://payment-method/complete?session_id={Uri.EscapeDataString(sessionId)}"))
            .ExcludeFromDescription();
        app.MapGet("api/v1/payments/customer/cancel", () =>
                Results.Redirect("agendame://payment-method/cancelled"))
            .ExcludeFromDescription();
        app.MapGet("api/v1/payments/provider/return", () =>
                Results.Redirect("agendame://provider-payout/complete"))
            .ExcludeFromDescription();
        app.MapGet("api/v1/payments/provider/refresh", () =>
                Results.Redirect("agendame://provider-payout/refresh"))
            .ExcludeFromDescription();
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IConfiguration configuration,
        StripeWebhookEventStore eventStore,
        PaymentAccountService paymentAccounts,
        IPaymentService payments,
        ProviderService providers,
        CancellationToken cancellationToken)
    {
        var secret = configuration["Payments:Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return Results.NotFound();

        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = request.Headers["Stripe-Signature"].ToString();

        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload, signature, secret, tolerance: 300, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return Results.BadRequest();
        }

        if (!await eventStore.TryBeginAsync(stripeEvent.Id, stripeEvent.Type, stripeEvent.Created))
            return Results.Ok();

        switch (stripeEvent.Data.Object)
        {
            case PaymentIntent intent:
                await payments.ReconcileAsync(intent.Id, intent.Status, stripeEvent.Created);
                break;
            case Session session when stripeEvent.Type == "checkout.session.completed"
                                      && !string.IsNullOrWhiteSpace(session.CustomerId):
                await paymentAccounts.CompleteCustomerSetupByStripeCustomerAsync(session.CustomerId, session.Id);
                break;
            case Account account:
                await providers.SetPayoutStateByConnectedAccountAsync(
                    account.Id,
                    new ProviderPayoutState(account.ChargesEnabled, account.PayoutsEnabled,
                        account.Requirements?.DisabledReason));
                break;
            case Refund refund when refund.Status == "succeeded" && !string.IsNullOrWhiteSpace(refund.PaymentIntentId):
                await payments.ReconcileAsync(refund.PaymentIntentId, "refunded", stripeEvent.Created);
                break;
            case Dispute dispute when !string.IsNullOrWhiteSpace(dispute.PaymentIntentId):
                await payments.ReconcileAsync(dispute.PaymentIntentId, "disputed", stripeEvent.Created);
                break;
        }

        await eventStore.MarkCompletedAsync(stripeEvent.Id);
        return Results.Ok();
    }
}
