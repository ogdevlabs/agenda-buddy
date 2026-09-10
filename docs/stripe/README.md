# Stripe Integration

AgendaMe uses Stripe Connect destination charges for marketplace payments. Customers save a reusable payment
method through Stripe-hosted Checkout, providers onboard to Express connected accounts, and AgendaMe creates a
manual-capture PaymentIntent when a provider accepts an appointment.

Use the runbook matching the Stripe environment:

- [Sandbox setup](sandbox.md) - local development or a deployed non-production environment using `sk_test_...`.
- [Production setup](production.md) - live-account activation, `sk_live_...`, production webhooks, rollout and
  rollback.

## Payment lifecycle

| AgendaMe action | Stripe operation | Expected result |
| --- | --- | --- |
| Customer adds a payment method | Customer + setup-mode Checkout Session + SetupIntent | Reusable PaymentMethod is stored by ID; AgendaMe stores only type, brand and last four digits |
| Provider configures payouts | Express connected account + Account Link | Both `charges_enabled` and `payouts_enabled` must be true |
| Customer requests a paid appointment | No charge | Server snapshots the service price and currency |
| Provider confirms | Off-session destination PaymentIntent with manual capture | `requires_capture`; 10% application fee and 90% destination allocation |
| Provider completes | Capture PaymentIntent | `succeeded` or `processing` |
| Provider cancels before capture | Cancel PaymentIntent | Full authorization release |
| Provider cancels after capture | Refund with transfer reversal and application-fee refund | Customer receives 100%; provider transfer and AgendaMe fee are reversed |

The API never accepts a client-supplied payment amount. Amount, currency, fee and provider destination are derived
from server-side appointment, service, customer and provider records.

## Configuration contract

Only `AgendaBuddy.Booking.Api` receives Stripe configuration.

| Runtime setting | AppHost parameter | Azure Key Vault secret | Format |
| --- | --- | --- | --- |
| `Payments:Stripe:ApiKey` | `stripe-api-key` | `stripe-api-key` | `sk_test_...` or `sk_live_...` |
| `Payments:Stripe:WebhookSecret` | `stripe-webhook-secret` | `stripe-webhook-secret` | `whsec_...` |

Never put either value in `appsettings*.json`, source control, issue text, logs or screenshots. Test and live keys
belong to different Stripe environments and must never be mixed. Each webhook endpoint has its own signing secret.

Gateway webhook URL:

```text
https://<gateway-host>/api/v1/payments/stripe/webhook
```

Without an API key, local AppHost runs use the non-charging recording gateway. Non-local environments fail
financial operations closed. The deployment workflow supplies `__UNCONFIGURED__` when Stripe secrets are absent
because Azure Container Apps rejects empty secret values; the application treats that sentinel as unconfigured.

## Implemented boundaries

- Stripe.net 45.0.0 and Accounts v1 Express onboarding.
- Stripe-hosted setup Checkout; the mobile app does not use or need a publishable key.
- Destination charges with manual capture and a fixed 1,000-basis-point application fee.
- One payment ledger record per appointment and attempt-scoped Stripe idempotency keys.
- Signed snapshot webhooks with a five-minute signature tolerance, durable event-ID deduplication and an
  out-of-order event timestamp guard.
- Provider readiness is refreshed directly from Stripe whenever the payment-account status endpoint is read.

## Current limitations

- The application accepts one webhook signing secret. Configure one platform-account snapshot destination for
  the required payment and Checkout events. A second connected-account destination needs separate-secret support
  before it can be relied on; provider status remains correct through synchronous refresh.
- Wallet availability depends on Stripe account, country, device and hosted Checkout eligibility. Apple Pay,
  Google Pay and PayPal are not yet certified for release; see Beads issue `agenda-buddy-83z`.
- Subscription-priced services are rejected. The implemented contract covers fixed and hourly services.
- There is no outbox or payment-operation retry worker. Failed application commands remain retryable, and Stripe
  webhook delivery supplies asynchronous reconciliation.

## Source map

- `AgendaBuddy.Library/Services/StripePaymentGateway.cs` - Stripe API calls.
- `AgendaBuddy.Library/Services/PaymentService.cs` - authorization, capture, release/refund and reconciliation.
- `AgendaBuddy.Library/Services/PaymentGatewayFactory.cs` - real/recording/unconfigured selection.
- `AgendaBuddy.Booking.Api/Modules/BookingModule.cs` - authenticated onboarding and account-status routes.
- `AgendaBuddy.Booking.Api/Modules/PaymentWebhookModule.cs` - public callback bridge and signed webhook handler.
- `AgendaBuddy.Booking.Api/Payments/StripeWebhookEventStore.cs` - webhook lease and deduplication.
- `AgendaBuddy.AppHost/AppHostWiring.cs` - local/cloud parameter injection.
- `.github/workflows/deploy.yml` - Azure Key Vault to Aspire parameter transport.

## Stripe references

- [Stripe testing](https://docs.stripe.com/testing)
- [Connect testing](https://docs.stripe.com/connect/testing)
- [Destination charges](https://docs.stripe.com/connect/destination-charges)
- [Webhooks](https://docs.stripe.com/webhooks)
- [API key management](https://docs.stripe.com/keys-best-practices)
