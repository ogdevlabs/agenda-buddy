---
feature: F-026
slug: provider-subscription
status: approved
approved-by: ogdevlabs (full-autonomy grant, see STATE.md 2026-08-26T23:12:00Z)
approved-date: 2026-08-27
---

# PRD: Provider Subscription (F-026)

## Problem

`CustomerEntity.SubscribedProviderCollection` (`List<string>?`) has never restricted a customer to
one provider — that half of the original filing was a documentation correction, not a code gap. The
real gap: no working subscribe capability exists at all. No route, no handler,
`AddCustomerCommandHandler` never sets the field, and `UpdateCustomerCommandHandler` actively
discards any client-supplied value for it — so even the generic `PUT /api/v1/customers/{email}`
can never change a customer's subscriptions.

## Users affected

Any Customer who wants to follow a Provider (e.g. their regular tutor or coach) for future
discovery/booking, and any Provider — see Design below for why this now also affects Providers.

## Requirements

- **R1.** A customer can subscribe to a provider by email; idempotent (subscribing twice is not an
  error and does not duplicate the entry).
- **R2.** A customer can unsubscribe from a provider by email; idempotent (unsubscribing from a
  provider never subscribed to is not an error).
- **R3.** A customer can list their own subscriptions.
- **R4.** Only the customer named in the path may subscribe/unsubscribe/list their own
  subscriptions (`OwnershipGuard.AssertOwner`) — the same rule `PUT /api/v1/customers/{email}`
  already enforces.
- **R5.** Subscribing to a provider email that does not exist fails (`404`) rather than growing the
  customer's list with a value nothing else can resolve.
- **R6.** Unsubscribing from a provider that no longer exists still succeeds — cleaning up the
  customer's own stale reference must never be blocked by the provider's absence.

## Design decision: wire both sides of the relationship (ADR-053)

`ProviderEntity.SubscribedCustomerCollection` already exists in the entity (`subscribed_customer_
collection`) and is already used in three integration test fixtures — but nothing has ever written
to it. `CustomerListRoleTest`'s own remarks name scoping `GET /api/v1/customers` to a provider's own
`SubscribedCustomerCollection` as "the stronger fix" for a real over-broad-read concern, "deferred,
not rejected" at a prior threat-model gate. Given the field already exists and the cost of keeping
it in sync is one more targeted write per subscribe/unsubscribe call, this PRD wires both
collections symmetrically rather than leaving `SubscribedCustomerCollection` permanently dead. See
ADR-053 for the full reasoning and what stays out of scope (no new Provider-side route; no change
to `GET /api/v1/customers`'s current behavior).

## Non-goals

- **A Provider-side "my subscribers" route.** `SubscribedCustomerCollection` is now kept accurate,
  but no new endpoint reads it yet. Filed separately: `agenda-buddy` beads issue (see episode).
- **Scoping `GET /api/v1/customers` to a provider's own subscriber list.** The deferred "stronger
  fix" `CustomerListRoleTest` names is a real behavior change to an existing route with its own
  threat-model weight — out of scope here, which only makes the underlying data correct.
- **Mobile UI.** `CustomersViewModel`'s "Browse and subscribe to providers" copy is not wired to any
  API call by this PRD — backend capability only, same split as F-022's `agenda-buddy-qe9`.

## Acceptance criteria

- AC1. `POST /api/v1/customers/{email}/subscriptions/{providerEmail}` returns `202` and adds
  `providerEmail` to the customer's `subscribedProviderCollection` and `email` to the provider's
  `subscribedCustomerCollection`.
- AC2. A second identical subscribe call is still `202` with no duplicate entry on either side.
- AC3. Subscribing to a nonexistent provider email returns `404` and does not touch the customer's
  list.
- AC4. `DELETE /api/v1/customers/{email}/subscriptions/{providerEmail}` returns `202` and removes
  the entry from both sides.
- AC5. Unsubscribing from a provider that no longer exists still returns `202` for the customer's
  own cleanup.
- AC6. `GET /api/v1/customers/{email}/subscriptions` returns the customer's own list; `403` for any
  caller other than that customer.
- AC7. `docs/api/openapi/Customer.json` is regenerated and drift-clean; the Bruno collection has
  request files for all three new routes.
