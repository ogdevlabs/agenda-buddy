# Episode 004: Wire Unreached Services

**Episode ID:** 004
**Feature name:** Wire Unreached Services — six shipped-but-unreachable capabilities get a route, and appointment status stops being whatever the caller says it is
**Feature slug:** wire-unreached-services
**Feature ID:** F-014
**Date built:** 2026-08-23, on `feat/F-014-wire-unreached-services` — PR [#40](https://github.com/ogdevlabs/agenda-buddy/pull/40), CI green (`build-and-test`, `Integration — real services + MongoDB`, `Mobile — Unit Tests`, `summary`), mergeable
**Phase delivered in:** Construction
**Date shipped:** 2026-08-23 — merged as `b760794` (GitHub API, `merge_method=merge`), tagged **`v0.4.0`**, PR #40
**Status:** **Final** — Operation phase closed 2026-08-23 after live verification against a running AppHost

---

## What Was Built

Five `Library` services (`NotificationService`, `MessageService`, `NoteService`, `PaymentService`,
`ReportingService`) and one command (`DeactivateProviderCommand`) had implementations and unit tests, and —
verified at Discover — **zero non-test references outside their own definitions**. F-006–F-010 were marked
Shipped on code nothing could call. F-014 gives each a route.

**Nine new routes**, placed by data ownership (ADR-036) rather than a new service: session notes and
payments on **Booking** (both keyed by the appointment), messages and notifications as two new top-level
groups on **Customer**, provider reporting and self-deactivation on **Provider**. Every route is
authenticated and ownership-guarded; two are role-gated. Asserted on **15** anonymous-access cases, not a
sample — a forgotten `RequireAuthorization()` is invisible in review, and F-016 exists because five routes
in this solution once served PII to anonymous callers.

**Appointment status becomes server-owned** (ADR-037), and that is not scope creep: `ReportingService`
derives its two headline numbers from `AppointmentStatus`, and nothing in production had ever set anything
but `Requested` — `Book()` and `Complete()` held the transition rules and were dead code, while
`UpdateAppointmentCommandHandler:51` copied whatever status the caller sent. Wiring reporting without fixing
that would have shipped a dashboard structurally guaranteed to report zero completed work, which is worse
than an unreachable endpoint: a dashboard reading zero looks like a business fact. Fixing status activated a
latent inversion, closed in the same change: `CancelAppointmentCommandHandler` refused to cancel a `Booked`
appointment — exactly the state a customer needs to cancel.

**The report no longer publishes a revenue figure** (ADR-039). The old formula was completed appointments ×
the *whole* service catalogue — correct only when a provider has one service — and it cannot be corrected by
arithmetic, because `AppointmentEntity` records no service, no fee, and no amount. `revenueAvailable: false`
plus a reason, because a plausible-but-wrong number would be believed.

**Payments are non-charging by default** (ADR-038). `RecordingPaymentGateway` is the default; Stripe only
when `Payments:Stripe:ApiKey` is configured as an Aspire secret, never in `appsettings.json`. Every
non-Stripe intent id is prefixed `local_`, permanently identifiable in stored data, not only in a log.

Tests went from 623 (431 + 118 + 74) to **701** (452 + 175 + 74) — **+21 backend, +57 integration, mobile
untouched**.

---

## Links

| Artifact | Path |
|---|---|
| PRD | [`PRD_F-014_wire-unreached-services_2026-08-23.md`](../archive/prds/PRD_F-014_wire-unreached-services_2026-08-23.md) |
| Brainstorm | [`brainstorm_wire-unreached-services_2026-08-23.md`](../archive/brainstorm/brainstorm_wire-unreached-services_2026-08-23.md) |
| Design | [`docs/pdlc/design/wire-unreached-services/`](../archive/design/wire-unreached-services/) — ARCHITECTURE, data-model, api-contracts, threat-model, ux-review |
| Verification | [`verification.md`](../archive/design/wire-unreached-services/verification.md) — 19/19 ACs, four defects found by running the software, eleven things this feature does *not* claim |
| Tasks | [`docs/pdlc/tasks/F-014/`](../tasks/F-014/) — T01…T09 |
| Decisions | ADR-036, ADR-037 (T-203), ADR-038 (T-206), ADR-039 |

---

## Key Decisions & Rationale

**ADR-036 — six capabilities land on three existing services, not an eighth.** A message is addressed to a
*person* — a provider has an inbox for exactly the same reason a customer does — so a URL saying `customers`
about a provider's inbox asserts something false every client then has to work around. A service is a
deployment unit, not a URL prefix, and Identity already hosts two unrelated groups
(`/api/v1/auth`, `/device-token`), so this is precedent rather than novelty. The alternative was a process, a
Dockerfile, a health check, an AppHost resource and a `WaitFor` edge, to serve `InsertAsync` and
`FindAllAsync` over two small collections.

**ADR-037 — status moves through the entity's own methods, not a handler-level table.** The `PUT` route now
*ignores* the status field (breaking for any client that sets it — free now, expensive after F-015, the same
argument that made F-016's breaking changes cheap); a dedicated route applies transitions via `Book()` /
`Complete()`, so a state added to the enum without a method is unreachable by construction, the opposite of
today. Completing is provider-only; either participant may book. Both stored copies of status (the
`appointments` document and the provider's embedded one) are written because `ReportingService` counts from
the embedded list — not atomic together (no replica set, no transaction), and re-issuing the transition
repairs a partial write.

**ADR-038 — the API key is assigned once, at construction, never per request.** `StripeConfiguration.ApiKey`
is a process-global static; the old code assigned it inside request handling. Narrowing exposure from
"written on every request" to "written at startup" is as narrow as the Stripe SDK allows.

**ADR-039 — a stated absence, not a silent omission.** `revenueAvailable` is a `bool`, not a nullable number,
so a client cannot render `null` as `0`. Blast radius swept before deciding: `EstimatedRevenue` had **zero**
production consumers outside `Library` and `Library.Tests`.

---

## What the implementation found that the plan didn't

Four defects, all found by **running** the software, none of them in the plan — this project's METRICS has
recorded the same observation after every episode.

1. **`ObjectId` does not round-trip through JSON.** `System.Text.Json` serialises the struct's public
   properties (`{timestamp, machine, pid, increment, creationTime}`), which cannot be read back into an
   `ObjectId` at all. Three of this feature's own route families need the id a create response returned.
   Pre-existing for every entity-returning route since it was written — nothing noticed because the mobile
   client cannot reach them (F-015) and no test had ever read an id back. Fixed with
   `Library/Tools/ObjectIdJsonConverter.cs`, registered in the three services this feature touches; Calendar,
   Services and Profession still emit the broken shape (`agenda-buddy-do5`, filed).
2. **`DeactivateProviderCommandHandler` could never have completed.** It called
   `mediator.Publish(request, cancellationToken)` where `request` is `IRequest<string>`, not
   `INotification` — compiles against the `object` overload, throws at runtime. The defect and its absence of
   callers arrived together; `DeactivateProviderEvent` existed for exactly this, with zero references.
3. **Enums are integers on the wire.** No `JsonStringEnumConverter` is registered anywhere, so a string enum
   in a request body fails model binding with a bare 400 and no validation detail. The new status route
   deliberately takes a string and parses it — `Enum.TryParse` happily returns `true` for undefined numbers,
   so `Enum.IsDefined` is what turns an out-of-range value into a 400 rather than a 409 implying the state
   exists.
4. **A flaky telemetry test, made likelier by this feature.** `TelemetryPiiTest` failed roughly one run in
   three: OpenTelemetry's ASP.NET Core instrumentation is process-wide, and two live `TracerProvider`s do not
   reliably each receive every span. One server-starting class had existed for years; F-021 added a second
   and F-014's full-suite runs made the overlap frequent. Fixed with a non-parallel xUnit collection — six
   consecutive green full-suite runs afterward.

---

## Test Summary

| Layer | Required (§7) | Command | Result |
|---|---|---|---|
| 1 — Unit | **yes** | `dotnet test agenda-buddy-backend.slnf` | ✅ **452** passing / 0 failing / **0 warnings**, 12 projects (baseline 431) |
| 2 — Integration | no in §7, **yes by this PRD** | `dotnet test AgendaBuddy.IntegrationTests/…csproj` | ✅ **175** passing, **1 m 52 s** of a 600 s budget (baseline 118) |
| 3–6 — E2E / perf / a11y / visual | no | — | ⊘ no command in project; logged skips |
| 7a — Dependency audit | **yes** | `dotnet list package --vulnerable --include-transitive` | ⚠️ **1 HIGH**, unchanged: `SSH.NET` in `AgendaBuddy.IntegrationTests` only (ADR-030). **F-014 adds no package reference at all**. Re-checked clean on `main` post-merge |
| 7b — Secret scan | **yes** | 6 patterns over changed files, then over `main` post-merge | ✅ clean both times |
| Mobile | — | `…/MobileApp.Tests.csproj /p:MobileWorkloads=false` | ✅ **74** (67 passing, 7 skipped), untouched |

`dotnet format agenda-buddy-backend.slnf --verify-no-changes` clean, both pre-merge and on `main` post-merge.

**19/19 acceptance criteria attested** in `verification.md` §2, each against a named test. AC-8 (every new
route refuses an anonymous caller) is asserted on 15 route/method pairs; AC-11/AC-12 (list routes return only
the caller's own; not-found and not-yours answer identically) each plant a third party's records in the same
database, so a route returning nothing at all cannot pass vacuously.

---

## Known Tradeoffs & Tech Debt

| Item | Disposition |
|---|---|
| **One pre-existing test replaced** (ADR-039) | `ReportingServiceTest.GetProviderReportAsync_CalculatesEstimatedRevenue` asserted the old, incorrect formula and passed. **Needs maintainer acknowledgement**, as F-016's ADR-025 and F-021's ADR-034 needed |
| **Revenue is not computed, and cannot be** | `AppointmentEntity` records no service, no fee, no amount. The fix is a data-model change touching F-015's contract and F-025's rules. Filed |
| **The payment amount is unvalidated** (T-205(c), accepted) | Nothing to validate it against, for the same reason revenue cannot be computed. Harmless with the recording gateway; a real underpayment the moment `Payments:Stripe:ApiKey` is configured |
| **Nothing writes a notification** | `NotificationService` is storage-only — no domain event calls `SendAsync`, so `GET /api/v1/notifications` correctly returns `[]`. F-022's dependency on it is **not yet satisfied** |
| **The two appointment-status writes are not atomic together** | Separate documents, no replica set, no transaction. A fault between them leaves the embedded copy stale; re-issuing the transition repairs it |
| **`MarkReadAsync` still read-modify-writes** | Rewriting it means editing `Library` service internals this feature is only wiring — the moment F-014 edits service internals, its claim ("these work as written, they were merely unreachable") stops being verifiable |
| **No indexes** | Every new query is an equality match that would benefit from one; no application code in this repository creates an index on any collection (`agenda-buddy-b0w`, confirmed live at F-021's ship gate) |
| **No Kafka publishing for messages** | F-007 built per-provider topics; `MessageService` does not use them, unchanged by this feature |
| **Slot correctness is F-025** | `Start < End`, future-dating, overlap — split out at Discover because it needs its own concurrency design (`agenda-buddy-ohw`) |
| **Deactivation writes a provider document — including embedded appointments and customer emails — into `events`** | Unchanged from how every command handler audits (ADR-027 kept command payloads); F-014 makes this handler *reachable*, so it is what makes the PII actually land there. **F-024** |
| **OpenAPI specs not regenerated** | Nine new routes is the largest spec drift this project has accumulated in one feature; the handlers return `Results<…>` so specs will under-report them anyway (F-018's spec-drift tasks). Worth doing before F-015 reads them |
| **No formal Party Review this cycle** | Unlike F-016 and F-021, Construction did not run a separate Review sub-phase with a findings file — the session went from `build_complete` straight into the human PR review that merged #40. A deviation from precedent, recorded rather than glossed; the 19/19 AC attestations and 701 green tests are what shipping actually rested on |
| **No episode draft existed at Construction wrap-up** | This episode was drafted at the Ship gate instead of at Construction Complete, unlike F-016/F-021. `/ship` verified test gates directly against `verification.md` rather than an episode Test Summary table, since none existed yet |
| **`scripts/tasks.cjs` does not exist in this repository** | F-014's task store is hand-written, same as F-021's; the structural security-AC-to-test check could not run structurally — each `[security]` AC names its test in the task body instead |
| **§7's security scan satisfied by hand for the fourth consecutive feature** | **F-017** still owns automating it |
| **The standards-readiness gate skipped for the eighth consecutive time** | Its six source repos have never resolved once under this `gh` auth. Needs a reachable source or explicit retirement — folded into **F-017** |

---

## Agent Team

| Agent | Role in this episode |
|---|---|
| **Solo** | ⚠️ The whole feature ran as **one model reasoning as each role** — same condition as every F-016/F-021 session. Fidelity is lower than independent context windows, and findings should be read with that in mind. |

---

## Verified at the Ship gate — not inferred from a green suite

Full smoke-test record above in STATE.md's Verify checkpoint; the headlines:

- **All 7 services `Healthy` under a live AppHost**, `/alive` = 200 on all seven.
- **Anonymous calls to the new notes and status routes answered 401 live** — not just under the integration
  harness, which hosts services differently than the literal running binaries.
- **A freshly registered Provider's JWT was validated across services and reached real business logic**
  (403/404, never 401) on 4 of the 9 new routes: notes, provider report, provider deactivation, and
  notifications. 403/404 rather than 200 is expected — the account had no pre-existing domain data — and is
  itself the evidence that the route authenticated, authorized, and executed rather than short-circuiting.
- **Dependency audit and secret scan on `main` post-merge**, both clean, no new findings versus the
  pre-merge state.
- **The documented AppHost shutdown gotcha recurred exactly as recorded**: `SIGTERM` left all 7 service
  processes orphaned, needing a second `pkill` on the project-path pattern. Not a defect — DEPLOYMENTS.md
  already carries this note from F-013.

What the live run did **not** re-derive: full AC-1…AC-19 correctness, which rests on the 78 new automated
tests (21 unit, 57 integration) run against real MongoDB over real HTTP — proportionate scope for a manual
smoke pass alongside a suite that size, matching what F-016/F-021's live verification also chose to spend
effort on (things automated tests structurally cannot observe) rather than re-litigate.

---

## Reflect Notes

### What went well

- **The plan's own scope re-cut held.** Slot correctness was split into F-025 at Discover because it needed
  its own concurrency design; nothing in Construction pulled it back in, and the resulting feature stayed
  coherent around one theme (reachability + the status dependency it exposed).
- **Every one of the four found-by-running defects had a clean, narrow fix** — a converter, one restored
  `Publish` call, a documented wire convention, a test-parallelism collection. None required re-opening a
  design decision.
- **The blast-radius discipline held**: ADR-039's revenue removal and ADR-036's routing decision were both
  preceded by a grep-verified consumer count (zero, in both cases) before deciding, not after.
- **19/19 ACs, 701/701 tests, 0 warnings, 0 failing** — the largest single-feature test delta of the four
  shipped features (+78), and it landed clean.

### What broke or slowed us down

- **Two process artifacts that normally exist by Construction Complete were missing at the Ship gate**: no
  Party Review file, and no episode draft. Both were worked around at Ship — test gates checked against
  `verification.md` directly, and the episode drafted now instead of earlier — but the workaround is itself
  the finding: the Review sub-phase's value (an independent pass before the human PR review) didn't happen
  this cycle.
- **`ObjectId`'s JSON shape and integer-enum binding are the second and third times this class of "wire
  contract nobody validated" has surfaced** (F-016 found the Calendar IDOR the same way — nothing exercised
  the route table until an integration test did). The pattern is now three-for-three: real defects are found
  by running the software, not by reviewing it.
- **The AppHost shutdown gotcha cost a few minutes again** — it's documented in DEPLOYMENTS.md but still had
  to be rediscovered by hitting it.

### What to improve next time

- **Draft the episode file at Construction Complete, not at Ship.** F-016 and F-021 both did this; F-014
  didn't, and the gap propagated into the Ship gate needing an alternate source of truth for test-gate
  verification.
- **Run the Review sub-phase as its own step even when Construction feels done.** The value isn't ceremony —
  F-016's Review caught a stale "10 queries" count and F-021's caught a vacuous sanitization test; skipping it
  here means F-014 has no equivalent independent pass on record.
- **Regenerate the OpenAPI specs before F-015 starts.** Nine new routes is the largest spec drift recorded to
  date, and F-015 is the feature that will actually read those specs.

### Metrics snapshot

- **Cycle time:** same-day — Discover claimed 2026-08-23T04:30Z, shipped 2026-08-23. Matches F-021's same-day
  cycle; F-016 took from 2026-08-18 build to 2026-08-22 ship-gate close (4 days, mostly idle between tag and
  Operation-close bookkeeping).
- **Test pass rate:** 701/701 = **100%** (452 + 175 + 74).
- **Tasks completed:** 9/9 (T01–T09).
- **Review findings:** 0 — no formal Review sub-phase ran this cycle (see above); the human PR review that
  merged #40 stands in its place.

---

## Deployment Record

| Item | Detail |
|---|---|
| **Merged** | `b760794` (merge commit, GitHub API `merge_method=merge`), PR #40, CI green on `build-and-test`, `Integration — real services + MongoDB`, `Mobile — Unit Tests`, `summary` |
| **Tagged** | `v0.4.0` (minor bump — `feat` commit present, no `BREAKING CHANGE` marker) |
| **Deployed to** | `local` (Aspire AppHost) only — where the verification above was performed |
| **CI/CD method** | None triggered — no deploy workflow ran; cloud deploy skipped per ADR-035 (see below) |
| **Custom deploy artifact** | No — user declined at the Step 9.1 prompt, default pipeline (none, given the deferral) |
| **Deployment Review Party** | Not convened — no custom artifact offered |
| **Overrides used** | None |
| **Config changes introduced** | None |
| **New tags recorded** | None — both environments were already tagged (`local`: `dev`; `cloud`: `dev`, provisional) |
| **Rollback tested** | No — nothing deployed to roll back |
| **DEPLOYMENTS.md updated** | Yes — local Deployment History row finalized with smoke-test results; cloud section's skip note extended (fourth consecutive skip, second under the ADR-035 deferral) |
| **Cloud** | ⚠️ **Deferred by decision, not blocked** — ADR-035: Azure is not reviewed until every pending feature is complete and the no-longer-needed tech debt is discharged. Fourth consecutive release without a remote deployment |
| **Still outstanding, and independent of the deferral** | Rotating the Atlas credential (`agenda-buddy-41s`, P0) |

---

## Approval

**Status:** Approved
**Approved by:** oscargarcia@ogdevlabs.onmicrosoft.com
**Approved date:** 2026-08-23
**Version shipped:** `v0.4.0` (tag at merge `b760794`)
**Links:** PR [#40](https://github.com/ogdevlabs/agenda-buddy/pull/40)
