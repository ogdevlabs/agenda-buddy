# Marketplace Payments Verification

## Automated

- Payment calculator: fixed/hourly, rounding and zero-decimal currency.
- Payment service: exact 90/10 allocation, manual authorization, capture, release, full refund and idempotent states.
- Booking integration against MongoDB Testcontainers: role-gated customer/provider onboarding, tokenized profile
  persistence, no client POST payment route, authorization on confirmation, capture on completion and provider
  cancellation release.
- Stripe webhook integration: invalid signature rejection, signed replay deduplication and completion tracking.
- Mobile: payment-account API/view models, route registration, app-link parser, localized XAML/resources, legal
  disclosures and provider-report metrics.
- AppHost/Gateway: payment secrets injected only into Booking, webhook/callback allowlist, non-local fail-closed mode.

Final local gates:

- Backend: 1,144 passed, 0 failed.
- Integration: 403 passed, 0 failed; final payment/webhook subset 22 passed after expiry enforcement.
- Mobile: 886 passed, 0 failed, 7 intentional live-Identity skips.
- iOS simulator and Android builds: passed.
- `dotnet format --verify-no-changes`: passed.
- Dependency audit: no vulnerable production package; accepted Testcontainers-only SSH.NET advisory remains per
  ADR-030. Gitleaks canary passed and the feature diff reported no leaks.

## iOS Simulator

- Provider registration reached Payout account; local setup changed backend state from required to ready and
  advanced to Professions. Profile exposed Payout account.
- Customer Profile exposed Método de pago. Setup changed backend state to Método de pago listo and rendered the
  structured mask as Visa terminada en 4242.
- A clean iOS bundle contained the `agendame` URL scheme. `simctl openurl` opened AgendaMe and routed from Dashboard
  to the Spanish customer payment view with live backend state.
- Provider report rendered Total de reservas, Completadas, Canceladas, Clientes únicos and the revenue explanation
  in Spanish.

## External Configuration Gate

Real card networks, PayPal, Apple Pay, Google Pay, Connect KYC/bank verification, disputes and payouts require a
Stripe sandbox/account configured with `Payments:Stripe:ApiKey` and `Payments:Stripe:WebhookSecret`. The repository
contains no credentials and does not claim those external rails were exercised locally. Production financial
operations fail closed until both the platform configuration and Stripe Dashboard method/country settings exist.

An anonymous Stripe claimable sandbox verified real API behavior for Customer creation, setup-mode Checkout,
reusable Visa test method attachment, manual PaymentIntent authorization (`requires_capture`) and cancellation
(`canceled`). Stripe refused the Accounts API with `invalid_request_error` because claimable sandbox keys have
limited permissions. Connect, destination transfer, PayPal and physical wallet activation remain tracked in
`agenda-buddy-83z`; deployed payment operations stay fail-closed until that account configuration exists.