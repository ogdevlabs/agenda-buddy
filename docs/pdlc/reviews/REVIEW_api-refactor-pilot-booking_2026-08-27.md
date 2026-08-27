# Review: API Refactor Pilot — Booking (F-019)
<!-- pdlc-template-version: 2.2.0 -->

**Task ID:** F-019 (T01–T11, all tasks)
**Task title:** API Refactor Pilot: Booking — Clean Architecture layering, MediatR dispatch, FluentResults, DataResponse<T>
**Date reviewed:** 2026-08-27
**Feature:** api-refactor-pilot-booking
**Feature branch:** `feat/F-019-api-refactor-pilot-booking`
**Episode:** (assigned after delivery)
**PRD:** [PRD_F-019_api-refactor-pilot-booking_2026-08-26.md](../prds/PRD_F-019_api-refactor-pilot-booking_2026-08-26.md)

---

## Reviewers

| Reviewer | Role | Present |
|----------|------|---------|
| Neo | Architect | yes |
| Echo | QA Engineer | yes |
| Phantom | Security Reviewer | yes |
| Jarvis | Tech Writer | yes |
| Muse | UX | no — `ux-review.md` triaged Skip, no UI surface in this feature |

All findings below have already been remediated in this same gate (fixed inline, not filed) except where
explicitly marked "filed." This review's findings and their fixes are cross-referenced against
`verification.md` §3.11, which carries the full technical narrative for each.

---

## Neo's Findings — Architecture & PRD Conformance

**PRD conformance:** Fully conformant, with 2 disclosed partial-conformance items (Requirement 6, 7 — both
pre-existing disclosures from T11, not new)
**Design doc adherence:** Followed, with corrections folded back into `api-contracts.md`/`verification.md`
as implementation diverged from the original prediction (this project's standing convention)

### Findings

**[MAJOR] `UpdateAppointmentCommandHandler`/`CancelAppointmentCommandHandler` depended on concrete
`ProviderService`/`BookingService`, defeating the point of the Clean Architecture split — FIXED**
Both handlers took the concrete classes as constructor parameters. Both interfaces (`IProviderService`,
`IBookingService`) already cover everything either handler calls — there was no `AppendAppointmentAsync`-
style gap here, unlike `Book`'s. Retyped to the interfaces. See Echo's matching finding below; fixed
together since both findings pointed at the same two files.

**[MAJOR] Response body echoes the client's forged `AppointmentStatus` on Update — FIXED**
`UpdateAppointmentCommandHandler.Handle` returned `Result.Ok(request.AppointmentEntity)` — the raw
deserialized request — instead of the entity actually fetched and persisted. The database write already
ignored the forged status (T-203's guarantee held), but the response lied about it. This is `agenda-buddy-2hd`,
independently found by Neo, Phantom, and Echo from three different angles (architecture correctness,
response-integrity, and untested-branch, respectively) — a real 3-way convergence, not restated three times
by coincidence. Fixed: `SearchAndUpdateAppointment` now returns the persisted `AppointmentEntity?`, and
`Handle` returns `Result.Ok(updated)`.

**[MINOR] Unused `IKafkaClient? kafkaClient` constructor parameter on all 3 moved handlers — FIXED**
"Reserved for future Kafka publishing," `#pragma warning disable CS9113`-suppressed, never read anywhere in
`Book`/`Update`/`Cancel`. YAGNI — removed from all three rather than left for F-020 to copy into six more
handlers.

**[MINOR] `StatusSpec`/`PaymentSpec` were dead code — FIXED**
Authored and unit-tested at T02, never wired into DI or a route. Wiring a no-op spec would have been
ceremony with nothing to show for it. Deleted; the 2 tests that tested them went with them (AC14's
now-deleted-code carve-out, not a violation).

**[INFO] `Booking.Infrastructure` staying empty is correct, not incomplete**
No Booking-specific repository need arose this feature. Confirmed as intentional YAGNI, matching
`ARCHITECTURE.md`'s prediction — see `verification.md` §4.

**[INFO] Mapster (ADR-049) has zero call sites in this feature**
Approved for this line of work but nothing in F-019 introduced a DTO mapping that needed it — `Book`'s
route still passes `AppointmentEntity` through directly (Requirement 7, already disclosed at T11). Not a
defect; recorded so F-020 doesn't assume Mapster usage exists as a pattern to copy from this feature.

---

## Phantom's Findings — Security

**OWASP Top 10 sweep:** Pass
**Auth & session security:** Pass — unchanged by this feature; `OwnershipGuard` calls stay exactly where
they were, in `Booking.Api`'s endpoint delegates, not moved into `Booking.Core`
**Input validation:** Issues found (see below) — one fixed here, one pre-existing and filed
**Secrets & credential handling:** Pass — nothing in this feature touches secrets

### Findings

**[HIGH] Update response leaking the caller's own forged status back to them — FIXED**
Independently confirmed as the same defect Neo and Echo found (`agenda-buddy-2hd`). Phantom's framing:
not a cross-tenant information-disclosure risk (a caller only ever sees their own submitted value echoed
back), but a real integrity-signaling defect — a client cannot distinguish "the server accepted my status
change" from "the server silently discarded it and echoed my request back anyway" by reading the response
alone. Fixed in the same edit as Neo's finding above.

**[MEDIUM] `NoteSpec`'s `.Required().NotEmpty()` would have shipped a whitespace-acceptance regression —
FIXED before it reached a route**
Caught during remediation, before wiring: Validot's `.NotEmpty()` accepts a whitespace-only string,
verified live against the real 2.6.0 assembly. Wiring the spec as originally authored would have let
`"   "` through where the inline `IsNullOrWhiteSpace` check it replaces currently rejects it — exactly
T-101's named threat (Validot strictness regression), caught before ship rather than after. Fixed to
`.NotWhiteSpace()`.

**[LOW] `POST /appointments` with a null `EmailProvider` surfaces as an unhandled 500, not a 400/404 —
pre-existing, filed, not fixed here (`agenda-buddy-cy2`)**
Confirmed unchanged by this refactor — the business logic moved verbatim, the bug predates F-019.
`BookingErrorLeakageTest` (T09) already proves the wire response leaks no exception detail regardless
(T-102's mitigation holds even though the status code itself is wrong). Out of scope for this feature's
own task list; correctly left filed rather than scope-crept into a fix here.

**Security sign-off:** No Critical findings. One High finding (response-integrity) — fixed in this gate.
One Medium finding (validation strictness) — fixed before it shipped. One Low finding — pre-existing,
correctly filed, not blocking.

---

## Echo's Findings — Test Coverage & Quality

**Acceptance criteria coverage:** All 14 covered (2 with disclosed partial-met/annotated verdicts — see
`verification.md` §2)
**Unit test coverage:** Adequate, after remediation (see Critical finding below — was a real gap before)
**Integration test coverage:** Adequate, after remediation (see Important finding below)
**E2E test coverage:** N/A — no E2E suite in this project
**Edge cases tested:** Yes, after remediation

### Findings

**[CRITICAL] `Update`/`CancelAppointmentCommandHandler`'s actual success/failure branches had zero unit
test coverage — FIXED**
Both handlers depended on the concrete `ProviderService`/`BookingService`, which Moq cannot mock without an
out-of-scope `Library` change — so their only existing unit tests were GuardClause-null checks. The exact
line `agenda-buddy-2hd` lived in (`Result.Ok(request.AppointmentEntity)` vs. the fix,
`Result.Ok(updated)`) had never been exercised by a unit test — only a live integration assertion would
have caught it, and at T11 that assertion had been deliberately weakened to a neutral field
(`Identifier`) specifically because the bug was filed, not fixed, at the time. Fixed alongside Neo's
matching architecture finding: retyped both handlers to `IProviderService`/`IBookingService`, added 8 new
Moq-based tests (4 each — success, no-such-provider, no-such-appointment, null-request).

**[IMPORTANT] `BookingErrorLeakageTest` only covered the *unhandled*-exception path (500); the *handled*
`Result.Fail` → `DataResponse<T>.Fail` mapping — T-102's other named path — had no dedicated test — FIXED**
Added `BookingANonExistentProvider_ReturnsBadRequest_WithTheHandlersFailureMessageInErrors`: forces
`BookAppointmentCommandHandler`'s real `Result.Fail` branch with a well-formed but non-existent provider
email (not a mocked `Result.Fail`), asserts a live 400 with `DataResponse<AppointmentEntity>.Success ==
false` and the handler's actual message text in `.Errors`.

**[INFO] A real regression was caught by the full integration suite, not assumed clean from a green
build**
Retyping the two handlers to interfaces left `IProviderService`/`IBookingService` unregistered in
`Booking.Api`'s DI container (only the concrete classes were registered). `dotnet build` stayed green — DI
resolution is a runtime concern, invisible at compile time. The full integration suite (no filter, per the
T06 lesson already on record in `verification.md` §3.7) failed exactly where it should: 6
`ServiceProvider` validation failures at every route behind either handler. This is exactly the kind of
regression a narrow `--filter` or trusting "the code compiles" would have missed — recorded as a positive
example of the process working, not a new finding requiring further action.

**[INFO] Restored assertion strength on `AC13_T203_ThePutIgnoresAClientAssertedStatus`**
Now that `agenda-buddy-2hd` is fixed, the integration test's assertion was strengthened back from a neutral
field (`Identifier`) to the actual `AppointmentStatus.Requested` check the AC's name implies — proving the
fix live, not just via the new unit tests.

---

## Jarvis's Findings — Documentation Completeness

**Inline code documentation:** Complete, after remediation
**API documentation:** Complete — `api-contracts.md` corrected for Requirement 6's actual state
**CHANGELOG entry drafted:** Yes — `CHANGELOG.md` created (did not exist before this feature)
**README updated (if needed):** N/A — no README changes needed for this feature

### Findings

**[MAJOR] `CLAUDE.md`'s Project Structure/Architecture sections were stale relative to this feature — FIXED**
Still described `Booking/` as a single project and pointed at `Booking/Program.cs`, a path this feature
deleted. Updated: Project Structure now documents the 4-way split (`Booking.Api`/`Booking.Core`/
`Booking.Domain`/`Booking.Infrastructure`); Architecture gained a new paragraph describing the layering,
`Result<T>`→`DataResponse<T>` mapping, and current Validot migration state; the Key Files entry now points
at `Booking.Api/Program.cs`.

**[MAJOR] `CLAUDE.md` had zero mention of FluentResults/GuardClauses/Validot despite all three being
load-bearing in this feature — FIXED**
Added a new Tech Stack bullet naming all three (and Mapster's zero-call-sites status) so a future session
doesn't have to rediscover these from the diff.

**[MINOR] `CLAUDE.md`'s test counts were stale even before this review's own fixes added more — FIXED**
484/301/950 → corrected to the actual current counts after Party Review remediation: 516/310/165 = 991.
(Verified live via `dotnet test`, not carried over from an earlier task's own count, which itself would
have been stale by 4 backend + 1 integration test after this review's fixes.)

**[MINOR] `CHANGELOG.md` did not exist — FIXED**
Created with an `[Unreleased]` entry covering this feature's Changed/Fixed/Known-issues, in Keep a
Changelog style, matching the repo's existing `v0.1.0`–`v0.7.0` tag history.

**[INFO] `verification.md`/`api-contracts.md` needed updating to reflect this review's own fixes — FIXED**
Both docs updated: `agenda-buddy-2hd` marked fixed (not filed) in `verification.md` §5 and a new §3.11
added narrating every Party Review fix; `api-contracts.md`'s Requirement 6 disclosure corrected from "1/10
routes" to "3/10 routes" (Book + the 2 note-content routes, after `NoteSpec`'s wiring).

---

## Builder's Notes

Two process notes worth carrying into F-020 (the next service to get this same treatment):

1. **Retyping a handler's constructor to an interface is not safe to verify by build alone.** DI
   registration is a separate, runtime-only concern from compile-time type-checking. The regression this
   review's own fix introduced (§ Echo's INFO finding above) would have shipped silently behind a green
   `dotnet build` if the full integration suite hadn't been re-run with no filter. F-020 should budget for
   a full-suite re-run after any interface-retyping pass, not just a build check.
2. **Before wiring any "obviously correct" validation spec, probe it live against the real library
   assembly first.** `.Required().NotEmpty()` reading like it should reject whitespace is exactly the kind
   of assumption this project's "reasoned, not observed" discipline exists to catch — and did, here, before
   it shipped rather than after.

All fixes in this review were applied under this session's standing full-autonomy directive ("stop asking
too many decisions, full autonomy, stop only after ship complete") — reviewer findings were independently
re-verified against the actual code before any fix was applied, per this project's stated culture of never
trusting a claim (including a reviewer's own) without checking the code first.

---

## Summary & Overall Recommendation

**Overall recommendation:** Approve

**Blocking issues (must fix before shipping):** None. The one Critical (Echo) and the 3-way-converged
finding at HIGH/MAJOR severity (Neo/Phantom/Echo on `agenda-buddy-2hd`) are both fixed and verified —
310/310 integration tests, 516/516 backend tests, 0 regressions, format-clean.

**Recommended fixes (strong advice):** None outstanding. All MAJOR/HIGH/CRITICAL findings from all 4
reviewers were fixed in this gate, not deferred.

**Deferred items (accepted for now):**
- `agenda-buddy-cy2` (Phantom, LOW) — null `EmailProvider` 500s instead of 400/404. Pre-existing, unchanged
  by this refactor, out of scope for the tasks that found it.
- `agenda-buddy-02e` (tracked from T11, description updated at this review) — Update/Cancel routes still
  validate via `MiniValidator`, not Validot. Requirement 6 now 3/10 routes migrated (up from 1/10), not
  10/10. Never assigned to any F-019 task.

---

## Human Decision

**Decision:** Approve

**Conditions / notes from human:** Self-approved under this session's standing explicit instruction ("stop
asking too many decisions, full autonomy, stop only after ship complete"). No Critical or unresolved
Blocker findings remain — every Critical/HIGH/MAJOR finding from all 4 reviewers was fixed and re-verified
in this same gate before this decision was recorded. Logged to `STATE.md`'s Guardrail Log per this
project's convention for autonomous judgment calls made under an explicit standing directive.

**Reviewed by:** ogdevlabs (git-configured identity)
**Date of decision:** 2026-08-27

---

## PR Comments

**Pushed to PR:** no
**Date pushed:** —
**PR link:** —
