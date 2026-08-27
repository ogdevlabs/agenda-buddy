# PRD: API Refactor Rollout

**Date:** 2026-08-27
**Status:** Draft
**Feature slug:** api-refactor-rollout
**Episode:** (assigned after delivery)

---

## Overview

This feature rolls the Clean Architecture pattern proven on Booking (F-019, shipped `v0.8.0`) across five of
the remaining six services — Calendar, Customer, Provider, Services, Profession. It is Stage 3 of 3 in the
API refactor program (ADR-014: F-018 built the verification harness, F-019 proved the shape on one service,
F-020 replicates it). Ending the "two styles in one codebase" state directly serves INTENT.md's maintainer
experience: after this feature, every service but Identity dispatches through the same real `mediator.Send`
mechanism, honoring CONSTITUTION §3 for the first time program-wide.

## Problem Statement

Five services — Calendar, Customer, Provider, Services, Profession — are still in Booking's pre-F-019 shape:
a `RequestCollection`/`IRequestCollection` class hand-constructs each command handler (`new
XCommandHandler(...).Handle(...)`), bypassing MediatR entirely despite `AddMediatR` being registered in every
one of them. None of their `EventAndCommands`-hosted handlers have a dedicated unit test — Provider's own
`RequestCollectionTest.cs` is a literal empty stub (`public void METHOD() {}`), so five services' worth of
command/query handlers have zero real coverage of their success/failure branching today. Booking now proves
the alternative shape works end-to-end, including a live production-traffic smoke test — this feature closes
the gap for the other five before it calcifies into "the two ways we do things here."

## Target User

Primarily this project's maintainer and its AI agents (same framing as F-018/F-019) — there is no
provider- or customer-facing behavior change. The real beneficiary is whoever builds the *next* feature
touching any of these five services: a consistent dispatch mechanism, real handler tests, and one response
envelope shape instead of two.

---

## Requirements

1. Each of the five services (Calendar, Customer, Provider, Services, Profession) gets the same 4-project
   Clean Architecture split Booking has: `<Service>.Api` (thin — endpoints/DI only), `<Service>.Core`
   (MediatR handlers), `<Service>.Domain` (commands/queries/DTOs, its own `DataResponse<T>`),
   `<Service>.Infrastructure`. `<Service>.Infrastructure` MAY stay empty per service (YAGNI, same as
   `Booking.Infrastructure`) unless a genuine infrastructure need surfaces during Build.
2. Every route on all five services MUST dispatch via real `mediator.Send(command, ct)` — zero
   hand-constructed handler calls anywhere under the five services' new `*.Api`/`*.Core` projects.
3. `<Service>/Requests/RequestCollection.cs` and `IRequestCollection.cs` MUST be deleted for all five
   services once their logic moves onto commands/handlers dispatched through MediatR.
4. Every command/query handler moved or newly authored in this feature MUST return `FluentResults.Result`/
   `Result<T>` instead of a string-sniffed `"exception"`-prefixed convention, mapped to each service's own
   `DataResponse<T>` at the API boundary — same shape as `Booking.Domain.Responses.DataResponse<T>`, one
   per service (not a shared package; F-019 validated the in-repo-per-project shape, ADR-049).
5. `GuardClauses` MUST be used for defensive null/argument checks in every new/moved handler in
   `<Service>.Core` — proven pattern from Booking's `Book`/`Update`/`Cancel` handlers.
6. Every handler's constructor MUST be typed against an existing interface (e.g. `IProviderService`) when
   one already covers everything the handler calls, not the concrete class — Booking's own Party Review
   found this was the difference between real Moq-based tests and GuardClause-only placeholders (Echo's
   Critical finding, fixed in `v0.8.0`). Where no interface exists or the interface doesn't cover a needed
   method (Booking's own `AppendAppointmentAsync` gap), stay on the concrete class and disclose why — do not
   add the missing method to `Library` as a side effect of this feature (out of scope, matches F-019's own
   restraint).
7. The real ASP.NET Core `CancellationToken` MUST be threaded from each route through to its handler and any
   handler-issued mediator dispatch — zero `new CancellationToken()` anywhere under the five services' new
   projects.
8. Zero string-sniffed control flow (`StartsWith("exception"` or equivalent) SHOULD remain anywhere in the
   five services' request-handling code once their handlers are moved (mirrors Booking's Requirement 5,
   downgraded to SHOULD here since — unlike Booking — some of these services' current inline checks may not
   use this pattern at all; verify per-service during Build rather than assume).
9. Validot MAY replace `MiniValidator.TryValidate` for routes where a validation spec is authored — this is
   NOT a blanket MUST. Booking's own Requirement 6 (Validot everywhere) reached 3 of 10 routes, not 10; this
   feature does not repeat that overreach. Each service's task list decides its own Validot scope explicitly,
   and any route left on `MiniValidator` is disclosed, not silently implied as covered.
10. `Mapster`-based request/response DTOs are explicitly OUT of scope for this feature (see Out of Scope) —
    Booking's own Requirement 7 was never assigned to any task and shipped with zero Mapster call sites;
    repeating an unvalidated requirement five more times is scope creep, not replication.
11. No handler ships with a placeholder/stub unit test (e.g. an empty `[Fact] public void X() {}`) as its
    only coverage — Provider's existing `RequestCollectionTest.cs` is exactly this anti-pattern and MUST be
    replaced with real tests, not carried forward as the new handler's test file.
12. The EventStore audit trail MUST survive unchanged in substance for every moved/rewritten handler —
    `eventStore.SaveAsync` per CONSTITUTION §3, matching each service's own pre-existing audit convention.
13. `EventStoreWriteGuardTest`'s `ScanRoots` MUST cover each of the five services' new `<Service>.Core`
    directory once handlers move there — same fix pattern as F-019-T03's `Booking.Core` addition.
14. Each service's existing Tier-1 route-contract test (`AgendaBuddy.IntegrationTests/Contract/
    <Service>RouteContractTest.cs`, from F-018) MUST pass with zero status-code assertion changes — route
    paths, verbs, and status codes do not change, only the response envelope and dispatch mechanism.
15. All backend + integration + mobile tests MUST pass with zero regressions in any service this feature
    does not touch (Booking, Identity, Gateway, AppHost, MobileApp).
16. No test file owned by any of the five services may be deleted except as an unavoidable, disclosed
    consequence of deleting the code it tests (same AC14 carve-out F-019 established) — Provider's stub test
    file is deleted and replaced, which satisfies this by writing real coverage in its place, not by
    leaving a gap.

## Assumptions

- Each of the five services' existing route paths, HTTP verbs, and request-body shapes stay unchanged —
  this is a dispatch-mechanism and response-envelope refactor, not a contract change (same posture as
  F-019's own Requirement 1/api-contracts.md finding).
- Each service's existing `Library` service dependencies (e.g. `ProviderService`, `CustomerService`) and
  their interfaces (`IProviderService`, etc.) are correct and complete enough for the handlers that need
  them — any genuine interface gap (like Booking's `AppendAppointmentAsync`) is disclosed per-service, not
  treated as a blocker requiring a `Library` change.
- Identity is out of scope (Discover 2026-08-27) — it has no `RequestCollection`/CQRS shape to replace, and
  migrating it is a different, larger, unvalidated feature with its own threat-model needs given F-021's
  deliberate exception-taxonomy decisions.
- `docs/api/openapi/*.json` for all five services stays semantically unchanged (route/verb/payload contract
  is unchanged) but will regenerate byte-for-byte differently if any internal type name changes — same
  drift-then-regenerate pattern F-019 hit twice.
- The five services can be migrated independently — there is no cross-service dependency between, say,
  Calendar's and Customer's migration order, so Construction can wave them in any order that minimizes risk
  (smallest/simplest first, informed by the Discover survey's route counts).

---

## Acceptance Criteria

1. For each of the five services: `<Service>.Api`/`<Service>.Core`/`<Service>.Domain`/
   `<Service>.Infrastructure` exist; `<Service>.Api` contains no business logic beyond DI/endpoint wiring
   (same carve-out class as Booking's `MongoDbConfiguration.cs`, if any exists per service). 🧪 test-first
2. `git grep "new.*CommandHandler(\|new.*QueryHandler(" <Service>.Api <Service>.Core` returns zero
   hand-constructed handlers, for each of the five services. 🧪 test-first
3. `<Service>/Requests/RequestCollection.cs` and `IRequestCollection.cs` no longer exist in the tree, for
   each of the five services. 🧪 test-first
4. Every one of the five services' routes returns a `DataResponse<T>`-shaped envelope on its success path,
   verified by a real HTTP request per route (not just a unit test) — same live-verification bar F-019's
   AC8 set. 🧪 test-first
5. `git grep "new CancellationToken()"` returns zero matches under any of the five services' new projects.
   🧪 test-first
6. Each service's `EventStoreWriteGuardTest` coverage (or the shared one, if `ScanRoots` is shared) includes
   every moved/new handler file for all five services — verified by the guard's own sanity-count assertion,
   not just code review. 🧪 test-first
7. Each of the five services' existing Tier-1 `<Service>RouteContractTest.cs` passes with zero status-code
   assertion changes. 🧪 test-first
8. Each of the five services' existing audit-trail integration test (where one exists, mirroring
   `BookingAuditTest.cs`) passes with zero assertion changes, or — where no such test exists yet for a
   service — a new one is added rather than the gap staying silently uncovered. 🧪 test-first
9. Zero handler across all five services has only a placeholder/stub test as its coverage — `git grep -A1
   "\[Fact\]" <Service>.Tests` shows no empty test bodies for any handler test. 🧪 test-first
10. Every handler retyped to an interface (per Requirement 6) resolves cleanly through DI — verified by a
    full (no `--filter`) integration-suite run, not a green build alone, per F-019's own Party Review
    lesson (a retyping fix there broke DI silently until the full suite caught it). 🧪 test-first
11. All backend + integration + mobile tests pass; `git diff main --name-only` confirms the entire feature's
    blast radius stays inside the five services' own directories plus expected shared infra/CI/docs touches
    — nothing in Booking, Identity, Gateway, or MobileApp. 🧪 test-first
12. `dotnet format agenda-buddy-backend.slnf --verify-no-changes` is clean.
13. No provider/customer-facing route path, verb, or request-body shape changed for any of the five
    services — verified against each service's committed OpenAPI spec (semantic diff, not byte diff).
    🧪 test-first

## User Stories

**F-020-US-01: A route on any of the five services dispatches through real MediatR**
*Acceptance criteria: 2, 4*
Given a request to any route on Calendar, Customer, Provider, Services, or Profession
When the route handler runs
Then it dispatches via `mediator.Send(command, ct)` — not a hand-constructed handler call
And the response body is wrapped in that service's `DataResponse<T>` envelope on success

**F-020-US-02: A handler previously untestable now has real coverage**
*Acceptance criteria: 6, 9, 10*
Given a command/query handler moved from `EventAndCommands` into one of the five services' `<Service>.Core`
When its constructor is typed against an already-sufficient interface (not the concrete class)
Then it is Moq-mockable, has real success/failure/audit-write unit tests, and resolves cleanly through DI
under a full integration-suite run

**F-020-US-03: A service's existing contract stays pinned through the refactor**
*Acceptance criteria: 7, 11, 13*
Given any of the five services' existing route/verb/status-code contract, pinned by its Tier-1 test
When this feature's handlers move and their dispatch mechanism changes
Then the Tier-1 test passes unchanged, the OpenAPI spec's routes/verbs/payloads are semantically identical,
and no other service (Booking, Identity, Gateway, MobileApp) regresses

**F-020-US-04: `RequestCollection` is fully retired for the five in-scope services**
*Acceptance criteria: 1, 3, 5*
Given `RequestCollection.cs`/`IRequestCollection.cs` exist today in Calendar, Customer, Provider, Services,
and Profession
When each service's migration completes
Then those files no longer exist, each service has the 4-project Clean Architecture split, and no
`new CancellationToken()` remains anywhere in the new projects

---

## Testing Approach: Test-Driven Development (TDD)

**Tests are written first.** During Construction (`/build`), for **every acceptance criterion above**, a **failing test is written and run before any implementation code** — the Red → Green → Refactor cycle:

1. **Red** — write the smallest failing test that pins the acceptance criterion, named with the Given/When/Then language from the matching user story. Run it; confirm it fails for the right reason (logic not implemented — not a syntax/import error).
2. **Green** — write the minimum implementation that makes the test pass. Run the test and the full suite; no regressions.
3. **Refactor** — clean up without changing behavior; suite stays green.

The build loop enforces this at a mandatory **TDD gate** (build Step 9a-bis): implementation code for a criterion may not be written until a failing test for it exists. The only exceptions are pure scaffolding, config-only, and infrastructure-only work — and even those require an **explicit human TDD override**. There is no silent skip. (TDD can be disabled only by editing `CONSTITUTION.md` § Test Gates — the Constitution always wins.)

**Security acceptance criteria are enforced mechanically (issue #55).** No `[security]`-tagged criteria are anticipated for this feature — it is a dispatch-mechanism/response-envelope refactor with no new attack surface (same posture Booking's threat model found: T-101/T-102 were the only "mitigate now" items, both about validation-strictness/error-leakage regressions in a refactor, not new capability). The Design sub-phase's threat-model triage will confirm this rather than assume it.

**Test layers** for this feature: **Unit** (required, CONSTITUTION §7) and **Integration** (run per this project's own convention, not required by §7 but never skipped in practice). E2E/Performance/Accessibility/Visual Regression remain not applicable — no command exists in this project for any of them, same as every prior feature. **Security** (Layer 7, always required): dependency audit + secret scan, unconditional.

---

## Non-Functional Requirements

- No new attack surface is introduced — same dispatch-mechanism/envelope refactor class as F-019, which
  Phantom signed off with 0 Critical findings.
- The CI budget (ADR-017's 600s tripwire) must stay comfortable — F-019's own integration suite ran ~4
  minutes with 310 tests; adding five services' worth of new handler tests should be sized against this
  budget during Plan, not discovered as an overrun at Test.
- `dotnet format --verify-no-changes` must stay clean throughout — F-019's Party Review hit one round of
  drift from new test files; budget for at least one auto-format pass per service.
- No performance regression on any of the five services' routes — the dispatch-mechanism change
  (hand-constructed call → `mediator.Send`) is not expected to be measurably slower, but this is asserted
  by the existing route-contract tests' pass/fail, not a dedicated benchmark (matches this project's
  existing NFR posture — no performance test command exists).

---

## Out of Scope

- **Identity.** Never adopted the `RequestCollection`/CQRS/EventStore shape; migrating it is introducing the
  pattern fresh, not replicating a proven one, and needs its own threat-model pass given F-021's deliberate
  exception-taxonomy decisions. Not filed as a future feature here — this is a scope narrowing, not a new
  commitment.
- **Mapster-based request/response DTOs**, for all five services. Booking's own Requirement 7 was never
  built (zero call sites, disclosed at F-019's Ship). Repeating it five more times without ever having
  validated what those DTOs should look like is scope creep this PRD explicitly declines.
- **A shared cross-service abstraction package** (e.g. a common `DataResponse<T>` or handler base). F-019
  validated the in-repo-per-project shape (ADR-049); extracting a shared package is a future decision, not
  this feature's — and only worth revisiting if five more repetitions of the same type make the duplication
  cost visible enough to justify it.
- **`agenda-buddy-02e`** (Booking's Update/Cancel routes still on `MiniValidator`) and **`agenda-buddy-cy2`**
  (Booking's null-`EmailProvider` 500) — both are Booking-scoped tech debt, not this feature's to fix.
- **Full Validot migration** for any service — per Requirement 9, this is a per-route decision made during
  Build, not a blanket mandate.
- **Any change to `Library`'s domain services or their interfaces** — a genuine interface gap (if found) is
  disclosed and worked around (staying on the concrete class), not closed by adding the missing method.

---

## Known Risks

- **Five services means five independent chances to repeat F-019's own found-and-fixed defects** (DI
  registration gaps from interface retyping, Validot whitespace-acceptance bugs, response envelopes
  echoing forged client input). Mitigated by carrying the 3 concrete lessons forward into Design/Plan
  (recorded in the brainstorm log's Discovery Summary) rather than re-discovering them per service.
- **`Customer` is the largest in-scope service (10 routes, 4 existing handler files, an existing
  `MessageRequest.cs`)** — the highest-risk single migration in this batch, both in surface area and in
  how much of its current shape is already known-different from Booking's (an extra request type). Plan
  should size Customer's task list larger than the other four, not assume uniform effort.
- **Provider's `RequestCollectionTest.cs` stub means Provider's actual current handler behavior has zero
  test-backed ground truth** — any behavior change during migration risks being silently wrong until a real
  test exists. Mitigated by Requirement 11/AC 9's explicit stub-replacement mandate.
- **`gh pr create` is confirmed non-functional under this repo's current `gh` identity** (READ-only access,
  discovered at F-019's Ship). This feature's own Ship will merge directly to `main` again, relying on the
  post-merge CI run rather than a pre-merge PR check — accepted, not re-litigated, unless the `gh` auth
  setup is fixed before this feature ships.

---

## Standards Alignment

_Not applicable — the Nordstrom standards-readiness gate is retired outright for this project (ADR-042).
Ten consecutive unreachable-source skips resolved into an explicit exemption: this is a personal project,
not a Nordstrom engagement. Step 6.5 was not run._

---

## Design Docs

- Architecture: [ARCHITECTURE.md](../design/api-refactor-rollout/ARCHITECTURE.md)
- Data model: [data-model.md](../design/api-refactor-rollout/data-model.md)
- API contracts: [api-contracts.md](../design/api-refactor-rollout/api-contracts.md)
- Threat model: [threat-model.md](../design/api-refactor-rollout/threat-model.md)
- UX review: [ux-review.md](../design/api-refactor-rollout/ux-review.md)

---

## Related Episodes

- [008: API Refactor Pilot — Booking](../episodes/EPISODE_api-refactor-pilot-booking_2026-08-27.md) — this
  feature replicates the pattern proven there; its Reflect Notes and Party Review findings are the direct
  source of Requirements 6/9/10/11 and the Known Risks above.
- [007: API Refactor Foundations](../episodes/007_api-refactor-foundations_2026-08-26.md) — the Tier-1
  contract tests and OpenAPI specs this feature's ACs verify against were built here.

---

## Approval

**Approved by:** ogdevlabs (self-approved under this session's standing full-autonomy grant — "stop asking
too many decisions, full autonomy, stop only after ship complete," extended to F-020 by the user's explicit
"now do F-20")
**Date approved:** 2026-08-27
**Notes:** No open questions raised at Discover that changed this PRD's content beyond the Identity scope
correction, which is already reflected in the Requirements/Out of Scope sections above, not left as a
pending revision.
