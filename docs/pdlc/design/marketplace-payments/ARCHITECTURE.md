# Marketplace Payments Architecture

## Boundaries

Booking owns payment orchestration because it owns appointment lifecycle and the payment ledger. Customer and
Provider profiles retain only non-sensitive Stripe references and readiness projections. Identity remains
credential-only. Stripe-hosted Checkout and Connect onboarding collect regulated payment, identity and bank data.

## Data Flow

1. Customer registration creates Identity and domain profiles, then routes to Payment method. Booking creates a
   Stripe Customer and setup-mode Checkout Session. Completion stores PaymentMethod id, type, brand and last four.
2. Provider registration routes to Payout account. Booking creates an Express connected account and Account Link.
   `account.updated` and status refresh project `charges_enabled`, `payouts_enabled` and disabled reason.
3. Booking resolves the selected provider service and snapshots amount/currency. Paid booking creation refuses a
   customer without a verified default payment method.
4. Provider confirmation authorizes off-session with `capture_method=manual`, `transfer_data.destination` and
   `application_fee_amount`. Only Stripe `requires_capture` advances the appointment to Booked.
5. Completion captures. Provider cancellation cancels an uncaptured intent before status mutation. A captured
   provider cancellation refunds with `reverse_transfer=true` and `refund_application_fee=true`.

## Reliability

- One unique payment row per appointment; operation-specific Stripe idempotency keys include authorization attempt.
- Payment states are append-safe enum values: Pending, Succeeded, Failed, Refunded, Authorized, Cancelled,
  RequiresAction and Disputed.
- Webhooks verify raw-body `Stripe-Signature`, acquire a durable five-minute processing lease, mark completion only
  after side effects, and ignore stale object events by Stripe creation time.
- Confirmation compensates persistence failure by releasing the hold and restoring Requested where possible.
- Cloud without Stripe keys uses `UnconfiguredPaymentGateway`; it never records synthetic success.

## Security And Privacy

- Amount, customer, payment method, destination and split are server-derived.
- No PAN, CVC, full bank number, SetupIntent secret or Account Link secret is persisted or logged.
- Gateway callback URLs are fixed from the request origin, preventing caller-supplied open redirects. Public HTTPS
  callbacks bridge to the registered `agendame://` mobile scheme.
- Signed webhooks are public by necessity and excluded from OpenAPI. Every account-management route uses JWT role
  and subject ownership.
- Financial records retain provider/customer tombstoned identity under existing erasure policy; Stripe object ids
  remain on ledger records where required for refunds, disputes and statutory accounting.

## Constraints

Card authorization is commonly valid for roughly five to seven days and PayPal timing differs. The implementation
records lifecycle state but automated near-service reauthorization remains policy-sensitive; long-horizon booking
must not promise an indefinite hold. Wallet and PayPal availability is controlled by Stripe account, currency,
country and device capability rather than hardcoded UI claims.