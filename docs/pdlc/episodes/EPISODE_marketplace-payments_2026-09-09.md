# Episode 018: Marketplace Payments

**Feature:** F-035 `marketplace-payments`
**Beads:** `agenda-buddy-jaq`
**Date:** 2026-09-09
**Version:** `v0.19.0`
**PR:** #153

## Problem

Payment was a caller-priced POST that could store a synthetic success. Customers had no reusable method, providers
had no payout account, and booking confirmation moved no money.

## Change

- Stripe-hosted customer payment setup and Express Connect provider onboarding, linked from registration/Profile.
- Immutable service pricing snapshots in integer minor units; subscription pricing remains explicitly unsupported.
- Provider confirmation authorizes a destination charge with a 10% application fee; completion captures.
- Provider cancellation releases the full hold before cancelling, or fully refunds after capture.
- Signed, leased, replay-safe and order-aware Stripe webhook reconciliation.
- Non-local no-key mode fails closed; local recording remains visibly synthetic.
- English/Spanish payment, payout, legal and provider-report UI, plus iOS/Android deep-link return handling.
- Removed the legacy POST payment route, command, service method and mobile amount form.

## Verification

- Backend: 1,144 passed.
- Integration: 403 passed; final payment/webhook subset 22 passed after expiry enforcement.
- Mobile: 886 passed, 7 intentional live-Identity skips.
- iOS and Android builds passed; iPhone 17 Pro simulator verified provider/customer onboarding, Profile entries,
	Spanish payment readiness/masking, callback routing and fully translated provider report.
- Anonymous Stripe sandbox verified setup Checkout, reusable card, manual authorization and release. Connect was
	explicitly denied by claimable-key permissions and remains operational task `agenda-buddy-83z`.
- Format, dependency audit and feature-diff gitleaks gates passed; ADR-030's Testcontainers-only SSH.NET advisory
	remains accepted.