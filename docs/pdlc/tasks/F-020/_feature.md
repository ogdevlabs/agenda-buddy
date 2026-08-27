---
id: F-020
title: api-refactor-rollout
status: in_progress
priority: 21
labels: [roadmap, "priority:21"]
depends_on: [F-019]
claimed_by: oscargarcia@ogdevlabs.onmicrosoft.com
created: 2026-08-18
updated: 2026-08-27
---
**Stage 3 of 3 in the API refactor program (F-018 → F-019 → F-020).** Roll the shape proven on `Booking` in F-019 across **five** of the remaining six services: `Calendar`, `Customer`, `Provider`, `Services`, `Profession`.

**Scope corrected at Discover 2026-08-27** (real current-state survey, run after F-019 shipped): `Identity` is **excluded**. It never adopted the `RequestCollection`/CQRS/EventStore shape the other 5 share with Booking's pre-F-019 state — it dispatches via direct `IdentityService` calls with zero MediatR, and F-021 deliberately gave it its own exception taxonomy. Migrating it would introduce the pattern fresh, not replicate a proven one — a different, riskier feature with its own threat model, not this one. The 2026-08-18 "six remaining `RequestCollection` classes" claim below predates F-019 and was wrong by one; corrected, not silently carried forward.

Deletes the five remaining `RequestCollection` classes (Calendar, Customer, Provider, Services, Profession). Ends the two-styles-in-one-codebase state F-019 deliberately created, for those 5. Whether shared abstractions get extracted into a common project is this feature's own decision, informed by what F-019 actually built (an in-repo `DataResponse<T>` per project, not a shared package — see ADR-049).
