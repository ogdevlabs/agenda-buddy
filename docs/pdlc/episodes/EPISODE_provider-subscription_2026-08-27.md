# Episode 012: Provider Subscription

**Episode ID:** 012
**Feature name:** Provider Subscription — a customer can now actually subscribe to a provider
**Feature slug:** provider-subscription
**Feature ID:** F-026
**Date built:** 2026-08-27, on `feat/F-026-provider-subscription`
**Phase delivered in:** Construction
**Date shipped:** 2026-08-27 — merged via the mandated PR path (ADR-050), PR #80, tagged **`v0.12.0`**
**Status:** Final

---

## What Was Built

`CustomerEntity.SubscribedProviderCollection` (`List<string>?`) never restricted a customer to one
provider — that half of the original filing was a documentation correction. The real gap: no working
subscribe capability existed at all. `AddCustomerCommandHandler` never set the field;
`UpdateCustomerCommandHandler` actively discarded any client-supplied value for it, so even the
generic `PUT /api/v1/customers/{email}` could never change it.

Three new routes on `AgendaBuddy.Customer.Api`, all ownership-gated to the customer named in the path:

1. **`POST /api/v1/customers/{email}/subscriptions/{providerEmail}`** — `202`, idempotent
   (`$addToSet`). `404` if the provider email doesn't exist.
2. **`DELETE /api/v1/customers/{email}/subscriptions/{providerEmail}`** — `202`, idempotent
   (`$pull`). Unsubscribing from a provider never subscribed to, or since deleted, is a no-op, not
   an error.
3. **`GET /api/v1/customers/{email}/subscriptions`** — the customer's own subscription list.

**Design decision made during Design, not carried in from the feature record (ADR-053): both sides
of the relationship get written, not just the customer's.** `ProviderEntity.SubscribedCustomerCollection`
already existed — unwired, permanently empty — and `CustomerListRoleTest`'s own remarks name it as
"the stronger fix" for `GET /api/v1/customers`'s over-broad read, "deferred, not rejected" at a prior
threat-model gate. Since the field already had the right shape, `SubscribeToProviderCommandHandler`/
`UnsubscribeFromProviderCommandHandler` now keep it in sync via two independent targeted
`FindOneAndUpdateAsync` calls (ADR-032) — no shared transaction, matching this codebase's existing
sequential-write convention for other two-collection updates. The two writes are deliberately
asymmetric on failure: subscribe requires the provider to exist (its `SubscribeCustomerAsync` call
doubles as the existence check); unsubscribe never lets a since-deleted provider block the customer's
own cleanup.

A real defect was caught by the new integration test, not shipped: `AgendaBuddy.Customer.Api`'s DI
registration (`ServiceCollectionExtensions.AddMongoDbRepository`) registered the concrete
`ProviderService` but never forwarded it to `IProviderService` — the interface the new handlers are
typed against. Would have been a runtime DI-resolution failure on the very first subscribe call, in
Production only (unit tests mock the interface directly, so they never exercised the container).
Fixed in the same PR.

**Also scoped, and deliberately not built here.**
- Mobile UI (`CustomerApiService` subscribe/unsubscribe methods, `CustomersViewModel` wiring) — filed
  as `agenda-buddy-q9m`, same split-scope shape as F-022's `agenda-buddy-qe9`.
- Scoping `GET /api/v1/customers` to a provider's own `SubscribedCustomerCollection` —
  `CustomerListRoleTest`'s "stronger fix" is now *possible* (the data is finally correct) but not
  implemented: it's a behavior change to an existing route for every caller, not a new one. Filed as
  `agenda-buddy-tbs`.
- A reciprocal Provider-side "my subscribers" route reading `SubscribedCustomerCollection` — no
  consumer needs it yet.

Suites: backend 568/568 (560 baseline + 8 new), integration 325/325 (317 baseline + 8 new, real
MongoDB container, including the DI-resolution proof above), 0 failures, 0 regressions.
`dotnet format --verify-no-changes` clean. `docs/api/openapi/Customer.json` regenerated and
drift-clean; three new Bruno request files.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-026_provider-subscription_2026-08-27.md`](../prds/PRD_F-026_provider-subscription_2026-08-27.md) |
| Feature record | [`docs/pdlc/tasks/F-026/`](../tasks/F-026/) |
| Decisions | ADR-053 |
| Follow-on | `agenda-buddy-q9m` (mobile UI), `agenda-buddy-tbs` (provider-scoped customer list) |
