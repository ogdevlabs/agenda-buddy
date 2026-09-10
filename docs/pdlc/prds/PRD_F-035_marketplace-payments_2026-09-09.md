# PRD: F-035 — Marketplace Payments

**Feature ID:** F-035
**Beads:** `agenda-buddy-jaq`
**Date:** 2026-09-09
**Status:** Construction complete

## Problem

The previous payment route accepted an amount chosen by the caller, immediately marked a local recording as
successful, had no customer payment method or provider payout account, and was disconnected from booking
confirmation. It could not implement a marketplace payment safely.

## Requirements

1. Customer onboarding requires a reusable Stripe payment method before requesting a paid appointment.
2. Provider onboarding requires a Stripe Connect payout account before accepting paid work.
3. Card, debit card, PayPal, Apple Pay and Google Pay are presented only where Stripe, the device and region
   support them; AgendaMe never receives full card or bank details.
4. Booking snapshots service identity, fee type, duration, ISO currency and integer minor-unit amount from the
   provider's service. The client cannot choose payment amount or currency.
5. Provider confirmation creates a manual-capture destination PaymentIntent. Ten percent of gross is the
   AgendaMe application fee and ninety percent is allocated to the provider.
6. Completion captures the authorization. Provider cancellation releases the full authorization before the
   appointment is marked cancelled; after capture it fully refunds and reverses transfer and application fee.
7. Every external mutation is idempotent. Signed Stripe webhooks reconcile setup, payment, refund, dispute and
   provider-readiness state and tolerate retries and event reordering.
8. Registration and Profile expose localized customer payment and provider payout views in English and Spanish.
9. A deployed environment without Stripe configuration fails financial operations closed. Local development may
   use visibly synthetic `local_` identifiers.

## Policy

- The 10% service fee is calculated from gross in integer minor units; Stripe processing fees are paid by the
  platform under the destination-charge model.
- Price is fixed at booking request time. A later service edit does not rewrite the appointment.
- Subscription-priced services are rejected until a separate recurring-billing policy exists.
- Ordinary authorization windows are finite. Product scheduling must not present an authorization as indefinite;
  expired holds require a new authorization before completion.
- Provider cancellation returns the customer to their pre-authorization position and earns neither party a fee.

## Acceptance Evidence

See [verification.md](../design/marketplace-payments/verification.md).