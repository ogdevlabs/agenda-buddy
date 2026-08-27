---
id: F-026
title: provider-subscription
status: shipped
priority: 26
labels: [roadmap, "priority:26"]
claimed_by: null
created: 2026-08-24
updated: 2026-08-27
---
Filed 2026-08-24, found while reviewing customer onboarding at the user's request, immediately after F-015 shipped. The user's framing — "a customer that after registration can subscribe to one or many providers, not only to one" — turned out to be correcting a documentation artifact, not a code constraint: `CustomerEntity.SubscribedProviderCollection` (Library/Entities/CustomerEntity.cs:33-34) is already a `List<string>?`, so the type has never restricted a customer to one provider.

The real gap: no working subscribe capability exists at all, for any cardinality.
- No `SubscribeToProviderCommand`, no handler, no `/subscribe` route on either Customer or Provider (`Customer/Program.cs`, `Provider/Program.cs` both checked — no such route).
- `AddCustomerCommandHandler` never sets the field on registration — it stays at its default `[]`.
- `UpdateCustomerCommandHandler.cs:19` actively discards any client-supplied value for this field, overwriting it with whatever the database already holds:
  `customerEntity.SubscribedProviderCollection = customer.SubscribedProviderCollection;`
  So even the generic `PUT /api/v1/customers/{email}` can never change a customer's subscriptions.
- `MobileApp`'s `CustomerApiService` has no subscribe method. `CustomersViewModel`'s UI copy ("Browse and subscribe to providers to book appointments.") is not wired to any API call.
- No test anywhere asserts single- or multi-provider behavior — the one existing assertion (`Customer.Tests/Auth/CustomerOnboardingAuthTest.cs:40-44`) only checks the field is non-null by default.

This is the same shape of defect F-014 fixed for six other capabilities (notes, payments, messages, notifications, reporting, deactivation): a domain field/intent exists but nothing wires it to a route. It was missed by F-014's sweep because F-003 (customer-onboarding-flow, the feature that should have delivered this) predates PDLC ship tracking, so it was never re-audited against the "implemented but unreachable" pattern F-014 was built to catch. F-003's own feature record (`docs/pdlc/tasks/F-003/_feature.md`) is a literally truncated sentence ending "...subscribes to one" — the probable origin of the single-provider assumption corrected here, not a deliberate design decision recorded anywhere else (no PRD for F-003 exists to check against; it predates PDLC).

Scope for a future /brainstorm: a subscribe/unsubscribe route (likely on Customer, symmetric with how messages/notifications are top-level groups there), a "my subscribed providers" list route, ownership (a customer can only manage their own subscriptions), idempotency (subscribing twice to the same provider), and whether Provider-side needs a reciprocal "my subscribers" view for anything (reporting? messaging eligibility?). No technical dependency on any other planned feature.
