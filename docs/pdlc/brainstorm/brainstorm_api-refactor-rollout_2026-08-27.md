---
feature: api-refactor-rollout
date: 2026-08-27
status: in-progress
last-updated: 2026-08-27T06:20:00Z
approved-by:
approved-date:
prd:
---

# Brainstorm Log: API Refactor Rollout (F-020)

**References the program-level log**: [`brainstorm_refactor-minimal-apis_2026-08-18.md`](brainstorm_refactor-minimal-apis_2026-08-18.md) — shared research for F-018/F-019/F-020, not re-derived here.
**References F-019's outcome**: [`EPISODE_api-refactor-pilot-booking_2026-08-27.md`](../episodes/EPISODE_api-refactor-pilot-booking_2026-08-27.md), [`REVIEW_api-refactor-pilot-booking_2026-08-27.md`](../reviews/REVIEW_api-refactor-pilot-booking_2026-08-27.md) — the proven pattern this feature replicates, and the real defects/lessons it should carry forward rather than re-discover.

## Divergent Ideation
_Not run — scope is fixed by the program decomposition (ADR-014); no alternative shape to diverge on._

## Socratic Discovery

Real current-state survey run across all 6 services the 2026-08-18 feature record named. Findings, per service:

| Service | Routes | Handler files (`EventAndCommands/Commands/<Svc>/`) | `RequestCollection`? | Dispatch | Validation | Exception handling | Handler unit tests |
|---|---|---|---|---|---|---|---|
| Calendar | 2 | 2 | Yes | Hand-constructed (`new XHandler(...).Handle(...)`) | 0 `MiniValidator` calls | `AgendaBuddyExceptionHandler` | 0 in `Calendar.Tests`; covered in `EventsAndCommands.Tests` |
| Customer | 10 | 4 | Yes (+`MessageRequest.cs`) | Hand-constructed | 2 `MiniValidator` calls | `AgendaBuddyExceptionHandler` + 2 local `catch(ForbiddenException)` | 0 in `Customer.Tests` |
| Provider | 6 | 6 | Yes | Hand-constructed, byte-for-byte Booking's pre-F-019 shape | 2 `MiniValidator` calls | `AgendaBuddyExceptionHandler` + local catches | 0 real — `RequestCollectionTest.cs` is an **empty stub** (`public void METHOD() {}`) |
| Services | 2 | 4 | Yes | Hand-constructed | 2 `MiniValidator` calls | `AgendaBuddyExceptionHandler` | 0 in `Services.Tests` |
| Profession | 2 | 2 | Yes | Hand-constructed | 0 `MiniValidator` calls | `AgendaBuddyExceptionHandler` | 0 in `Profession.Tests` |
| **Identity** | 5 | **0 — not in `EventAndCommands` at all** | **No** | Direct `IdentityService` method calls, **zero MediatR/CQRS** (`AddMediatR` registered but unused by any route) | Inline manual checks | **Bespoke**: own exception taxonomy (`AuthValidationException`, `ConflictException`, etc.) + generic `UseExceptionHandler`, not `AgendaBuddyExceptionHandler` | 0 |

All 6 services already have a Tier-1 route-contract test (F-018) and a committed OpenAPI spec — both survive this feature untouched, same as F-019.

## Adversarial Review

**The 2026-08-18 feature record's "six remaining `RequestCollection` classes" claim does not hold.** It's **five** — Calendar, Customer, Provider, Services, Profession share Booking's exact pre-F-019 shape (hand-constructed handler dispatch, `EventAndCommands`-hosted handlers, no per-service handler tests). **Identity is structurally different, not further along or behind** — it never adopted CQRS/EventStore/RequestCollection at all, and F-021 deliberately hardened its error handling with its own exception taxonomy (a T-001-linked decision, not an oversight). Migrating Identity to the Clean Architecture shape would mean *introducing* the pattern fresh — a materially larger, riskier, differently-scoped change than replicating what F-019 already proved. Bundling it into F-020 would silently inflate scope past what was actually validated.

**Decision (self-made under this session's standing full-autonomy grant, logged to STATE.md's Guardrail Log): F-020's scope is corrected to 5 services — Calendar, Customer, Provider, Services, Profession. Identity is explicitly out of scope for this feature.** If Identity's own CQRS migration is ever wanted, it is a future feature in its own right (not yet filed — this is a scope *narrowing*, not a new commitment), since it would need its own threat-model pass given F-021's deliberate auth-error-handling decisions.

**Secondary finding carried into Plan:** Provider's existing `RequestCollectionTest.cs` is a dead stub (empty test methods) — F-019's own Party Review found the same shape of problem (GuardClause-only placeholder tests hiding untested branches) and fixed it. F-020's tasks should replace stubs with real tests from the start, not just delete-and-move.

## External Context
_None ingested._

## Discovery Summary

**F-020 rolls Booking's now-proven Clean Architecture split (F-019, `v0.8.0`) across 5 services — Calendar, Customer, Provider, Services, Profession — not 6.** Identity is excluded: it never adopted the CQRS/`RequestCollection` shape F-019 replaced, so migrating it would be a fresh introduction of the pattern, not a replication, and is out of scope. Each of the 5 in-scope services is confirmed to be in Booking's exact pre-F-019 shape (hand-constructed handler dispatch, no MediatR routing, no handler-level unit tests, `MiniValidator` where any validation exists at all). All 6 services already have Tier-1 contract tests and committed OpenAPI specs, both unaffected by this feature (route/verb/payload contracts do not change, matching F-019's own Requirement 1). Carries forward 3 concrete lessons from F-019's Party Review: (1) retype any handler constructor to the interface it actually needs, verified against DI registration with a full-suite run, not a green build; (2) probe any new/moved Validot spec live against the real assembly before wiring it; (3) don't ship placeholder tests (Provider's empty `RequestCollectionTest.cs` stub is this project's second sighting of that exact anti-pattern).
