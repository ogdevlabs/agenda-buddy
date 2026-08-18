# PRD: Secure Public Endpoints

**Date:** 2026-08-18
**Status:** Approved
**Feature slug:** secure-public-endpoints
**Feature ID:** F-016
**Episode:** _assigned after delivery_

> **First feature of the Platform Remediation program** (F-016 → F-021 → F-014 → F-015 → F-017).
> Program Discover: [`brainstorm_platform-remediation_2026-08-18.md`](../brainstorm/brainstorm_platform-remediation_2026-08-18.md) — approved 2026-08-18.

---

## Overview

Five endpoints serve personally identifiable information to completely unauthenticated callers, and two more are authenticated but not ownership-scoped. The worst, `GET /api/v1/providers`, returns every provider's full record — including each provider's embedded appointment book and subscribed-customer list — to anyone who asks, unpaginated. `INTENT.md` positions Agenda Buddy as the place independent coaches, tutors and therapists keep their client records; those records are currently world-readable.

This feature closes that exposure **and builds the instrument that proves it closed.** The second half is not optional padding: `docs/pdlc/context/11-testing.md:148` establishes that `Program.cs` is not coverable, that no integration test exists anywhere in the solution, and therefore that *"every endpoint's auth attribute, validation call, ownership guard, and status-code mapping is unverified end-to-end."* Endpoint authorization is precisely what this feature changes, and it is the one thing this codebase currently cannot observe.

---

## Problem Statement

**The exposure.** Six anonymous `GET` routes exist. Five carry PII:

| Route | Anchor | Exposes |
|---|---|---|
| `GET /api/v1/providers` | `Provider/Program.cs:132-147` | Every provider's full record. `ProviderEntity.cs:38-42` embeds `ServiceEntities`, `AppointmentEntities` (each carrying `email_customer`) and `SubscribedCustomerCollection` — so one anonymous request yields every provider's entire appointment book and client list. Unpaginated. |
| `GET /api/v1/customers` | `Customer/Program.cs:146-158` | Every customer record — names and email addresses. Unpaginated. |
| `GET /api/v1/providers/{email}` | `Provider/Program.cs:150-167` | One provider's full nested graph. |
| `GET /api/v1/customers/{email}` | `Customer/Program.cs:160-172` | One customer record; also an **email-enumeration oracle** (200 vs 404). |
| `GET /api/v1/services/{email}` | `Services/Program.cs:94-111` | A provider's service catalogue and fees. |
| `GET /api/v1/professions*` | `Profession/Program.cs:123,136` | Reference data — **defensibly anonymous, out of scope.** |

**The IDOR.** `GET /api/v1/calendar/{availability,appointments}/{email}` (`Calendar/Program.cs:93-141`) calls `RequireAuthorization()` but never `OwnershipGuard`. A valid token proves the caller is *somebody*, not that `{email}` is theirs — so **any registered user can read any provider's full appointment list**, customer emails included. Its sibling services all guard (`Provider/Program.cs:182`, `Customer/Program.cs:133`, `Services/Program.cs:122,146`); Calendar is the one family that forgot.

**The dead authorization layer.** `OwnershipGuard.AssertRole` is **never called anywhere** (`13-security.md:137`). The `role` claim is minted, validated, and authorizes nothing. Consequently any authenticated Customer can `POST /api/v1/providers` to create a provider record for an arbitrary email, and any Customer can `POST /api/v1/professions` to write to the global reference catalogue.

**The silent-500 trap.** `ForbiddenException.StatusCode => 403` is never read. Correct 403s depend on every endpoint hand-writing `try { OwnershipGuard… } catch (ForbiddenException) { return TypedResults.Forbid(); }` — duplicated at 8 call sites with **no compile-time protection**. A new guarded endpoint that omits the `try/catch` returns **500 instead of 403**.

**The amplifier.** `GetProvidersQueryHandler.cs:23` serialises the *entire* provider list — every provider, every embedded appointment, every customer email — into a MongoDB document **on every anonymous `GET /api/v1/providers` call**. The `events` collection therefore accumulates unbounded, unindexed, never-pruned copies of the whole dataset. Fixing the endpoint without fixing this leaves the copies.

**Why this cannot currently be verified — the root cause.** 24 tests cover the 26-line `OwnershipGuard` class (`11-testing.md:72`) and prove the guard function is correct. **Nothing checks whether an endpoint calls it.** The Calendar IDOR exists *because* of that gap. Shipping this feature on unit tests alone would reproduce the exact conditions that produced the bug it fixes.

---

## Target User

**Primary — the independent service provider** (`INTENT.md` persona: fitness coach, tutor, therapist, software instructor). Their client list, contact details and appointment history are the commercial and confidential core of their practice, and are currently readable by anyone who can reach the API.

**Also directly affected — that provider's clients.** `CustomerEntity` holds names and emails; `AppointmentEntity` links a named customer to a named provider at a time. For a therapist or coach, the *association itself* is sensitive information about a third party who never agreed to publish it.

**Secondary — the maintainer.** After this feature they can change endpoint authorization and get a real answer about whether it worked. Today they cannot, which is a standing tax on every future security change and on F-014, F-015 and F-021 in particular.

**Explicitly not for:** anonymous/public consumers. The decision that discovery is a post-signup activity (see Assumptions) removes them as a user class for these routes.

---

## Requirements

### A. Verification harness — wave 1, absorbed from F-018's approved plan

1. The `EventAndCommands/Persitency/` directory and its three namespace declarations MUST be renamed to `Persistence`, and this MUST land as its **own commit before any integration test is authored**, so no test is written against the misspelled namespace. *(Inherited F-018 AC-16. Scope measured: 11 files, one reference each; zero references in any `.json`/`.yml`/`.csproj`/`.slnf`; behaviour-preserving because the collection name comes from `EventsCollection` config, not the namespace.)*
2. An `AgendaBuddy.IntegrationTests` project MUST exist, and all seven services MUST expose internals to it via `InternalsVisibleTo`.
3. The harness MUST generate its RSA keypair per test session, in memory, and MUST NOT write PEM or private-key material to disk at any point.
4. The harness MUST start a real service over HTTP against a MongoDB **Testcontainer**, using one container per test class with a unique database per test.
5. The harness MUST **fail closed**: if the resolved MongoDB connection string does not target a Testcontainer-managed endpoint, it MUST refuse to run and MUST fail with a message naming the offending host. Under no circumstances may an integration test reach a real cluster.
6. The harness MUST provide a token factory producing valid, expired, and foreign-subject RS256 tokens.
7. The harness MUST report actionable diagnostics when the container runtime is unavailable, rather than failing as an opaque timeout. *(Rancher Desktop places `docker` at `~/.rd/bin`, off `PATH`; Testcontainers shells out to it.)*
8. Every authorization change in section B MUST be covered by an integration test that exercises the **real route** over HTTP — not the guard function in isolation.

### B. Exposure closure

9. `GET /api/v1/providers`, `GET /api/v1/providers/{email}`, `GET /api/v1/customers`, `GET /api/v1/customers/{email}` and `GET /api/v1/services/{email}` MUST require authorization.
10. `GET /api/v1/providers` and `GET /api/v1/providers/{email}` MUST NOT return the embedded `AppointmentEntities` or `SubscribedCustomerCollection` to a caller who is not the owning provider. **This holds regardless of the authentication decision** — an authenticated customer browsing providers has no business receiving every provider's appointment book.
11. Both `GET /api/v1/calendar/availability/{email}` and `GET /api/v1/calendar/appointments/{email}` MUST call `OwnershipGuard` so that `{email}` must belong to the caller.
12. `POST /api/v1/providers` MUST enforce that the caller holds the `Provider` role, and MUST NOT permit creating a provider record for an email that is not the caller's own.
13. `POST /api/v1/professions` MUST enforce a role check so an arbitrary authenticated Customer cannot write to the global reference catalogue.
14. `ForbiddenException` MUST map to HTTP 403 **centrally**, such that an endpoint that omits a local `try/catch` still returns 403 rather than 500. The 8 existing hand-written call sites MUST NOT double-handle it.
15. `GET /api/v1/providers` and `GET /api/v1/customers` MUST paginate. The response shape MUST be **recorded as a contract decision in `DECISIONS.md`**, because F-015 is written against it.
16. Query handlers MUST NOT serialise their full result payload into the `events` collection. `GetProvidersQueryHandler.cs:23` is the specific offender; the audit record MUST retain enough to be useful (operation, status, timestamp) without copying the dataset.
17. `GET /api/v1/professions` and `GET /api/v1/professions/{name}` MUST remain anonymous — reference data, seeded from `ProfessionSeedData.cs`, no PII.
18. `AssertOwner`'s null-claim behaviour SHOULD be left to F-021 unless section B work touches that file first, in which case fixing it here is cheaper than deferring. *(Recorded so it cannot fall between the two features.)*

### C. Non-regression

19. The full backend suite MUST stay green: **305 backend tests across 12 projects**, plus 74 mobile (67 passing, 7 skipped) = 379 project total. No test may be deleted to make this feature pass.
20. No endpoint's success-path response semantics may change except as required by requirements 10 and 15.

---

## Assumptions

- **The cluster holds synthetic/development data only.** Maintainer-confirmed 2026-08-18. This is therefore exposure *prevention*, not breach response — there is no notification duty and no GDPR clock. If that ever stops being true, this feature's urgency changes and so does F-024's.
- **No client depends on anonymous access to these routes today.** The only client is `MobileApp`, and it cannot reach any domain route: every path omits `api/v1/` and the single `ApiBaseUrl` cannot address seven processes (`01-api-surface.md:140-158`). Requirements 9 and 15 are therefore breaking changes with **zero live consumers** — the cheapest possible moment to make them.
- **Provider discovery is a post-signup activity.** `ROADMAP.md` F-003 `customer-onboarding-flow` — status **Shipped** — defines its own flow as *"a customer **signs up**, discovers providers, and subscribes to one."* Authenticating provider listing therefore matches the product's shipped definition rather than regressing it. **This is the one assumption that is a product call and is flagged for explicit confirmation at approval.**
- **Testcontainers works on this machine with zero configuration.** Spike-proven under F-018 on Rancher Desktop. It measured **4.45 s warm container startup** against the 1–3 s originally assumed, which is why the design is container-per-class rather than container-per-test (ADR-017).
- **The Rancher VM can host it.** 2 CPUs / 4.1 GB, already running a k8s cluster. Container-per-class across this feature's test classes is believed to fit; this is the least certain assumption in the list.
- **`ProviderEntity`'s embedded shape stays as-is.** Requirement 10 is solved by projecting at the read boundary, not by restructuring the document — that would be a data-model migration and belongs to F-019/F-020.

---

## Acceptance Criteria

**Harness**

1. `EventAndCommands/Persistence/` exists, no `Persitency` identifier remains anywhere in the tree, and the rename is a single isolated commit that precedes every integration-test commit. 🧪 test-first
2. `dotnet test` discovers and runs `AgendaBuddy.IntegrationTests`, and all seven services grant it `InternalsVisibleTo`. 🧪 test-first
3. No PEM or private-key material appears in any tracked file, and no production `.csproj` references `AgendaBuddy.IntegrationTests`. 🧪 test-first
4. An integration test starts a service, issues a real HTTP request to a real route, and asserts the response — against a MongoDB Testcontainer, one container per test class, unique database per test. 🧪 test-first
5. Given any resolved MongoDB connection string that does not target a Testcontainer-managed endpoint, the harness refuses to run and fails with a message naming the offending host. Verified by exporting `ConnectionStrings__mongodb` to a non-container value and observing the abort **before** any test executes. 🧪 test-first
6. A request bearing an **expired** token returns **401**; a request bearing a valid token whose subject is a **different user** returns **403**. Both asserted against a real route. 🧪 test-first
7. With the container runtime unreachable, the harness fails with a message that names the runtime problem and the remedy — not a bare timeout. 🧪 test-first

**Exposure closure**

8. Each of the five routes in requirement 9 returns **401** to an unauthenticated request. 🧪 test-first
9. `GET /api/v1/providers` and `GET /api/v1/providers/{email}`, called by an authenticated caller who is **not** the owning provider, return a payload containing **no** `AppointmentEntities` and **no** `SubscribedCustomerCollection`. 🧪 test-first
10. `GET /api/v1/calendar/availability/{email}` and `GET /api/v1/calendar/appointments/{email}` return **403** when `{email}` is not the caller's, and **200** when it is. 🧪 test-first
11. `POST /api/v1/providers` returns 403 for a caller holding only the `Customer` role, and 403 when a `Provider` attempts to create a record for an email that is not their own. 🧪 test-first
12. ~~`POST /api/v1/professions` returns 403 for a caller who does not hold the required role.~~ **STRUCK at the Plan readiness party, 2026-08-18.** This criterion **contradicts ADR-025**, which deletes the route rather than role-gating it — there is no admin role to gate on. Leaving it would have sent Build to implement a role check on a route that no longer exists. **Replaced by AC-26** (`[security]`, T-007): the route returns 404/405 and the profession *read* routes still return 200 anonymously. Caught by Echo's traceability matrix; see the Readiness Assessment. 🚫 superseded
13. A route that throws `ForbiddenException` **without** a local `try/catch` returns **403**, not 500. Demonstrated by a test-only endpoint or by removing one existing `try/catch` and asserting the status is unchanged. 🧪 test-first
14. The 8 existing hand-written `ForbiddenException` catch sites still return exactly one 403 — no double-handling, no changed body. 🧪 test-first
15. `GET /api/v1/providers` and `GET /api/v1/customers` accept pagination parameters, return a bounded page with total-count metadata, and cap page size at the documented maximum even when a larger value is requested. 🧪 test-first
16. The paginated response shape is recorded in `DECISIONS.md` as an ADR before the endpoint work is marked done. 🧪 test-first
17. After a `GET /api/v1/providers` call, the newly written `events` document does **not** contain any provider email, customer email, or appointment record. **Broadened at Plan:** the same must hold for **all ten** query handlers, not just the provider path — `GetCustomersQuery` serialises every customer record and is an equivalent hole (ADR-028). 🧪 test-first
18. `GET /api/v1/professions` and `GET /api/v1/professions/{name}` still return 200 to an unauthenticated request. 🧪 test-first

**Non-regression**

19. `dotnet test agenda-buddy-backend.slnf` reports **305 or more** passing, 0 failing, 0 warnings; `MobileApp.Tests` reports 74 (67 passing, 7 skipped). No pre-existing test was deleted or skipped to achieve this. 🧪 test-first

**Threat-derived `[security]` criteria — logged addendum, added at Plan Step 14.5 (see ADR-029)**

Threat modelling runs at Design Step 10.5, *after* the Define gate closed, so these seven are an expected and auditable addition rather than a Define reopen. Each is materialized as a structured `[security]` AC on its task via `tasks.cjs ac add`, which is what makes the guarantee mechanical: the build TDD gate enumerates `[security]` ACs and demands a failing-first test named after the threat id, and `tasks.cjs done` **refuses to close the task** until a test is linked. A threat recorded only as a task-body citation is invisible to both.

20. `[security]` **(T-002, CRITICAL)** Given `ConnectionStrings__mongodb` is exported to a value that is not the endpoint reported by the fixture's own Testcontainer, the integration suite aborts during fixture construction with a message naming the offending host, and **no database or collection is created**. → `F-016-T06` 🧪 test-first
21. `[security]` **(T-001, HIGH)** Given a valid token carrying no `NameIdentifier`/`sub` claim, `GET /api/v1/providers/{email}` never returns the full `ProviderEntity`; and `OwnershipGuard.AssertOwner(user, null)` throws `ForbiddenException` rather than returning. → `F-016-T09` 🧪 test-first
22. `[security]` **(T-003, HIGH)** Given a valid token whose only role is `Customer`, `GET /api/v1/customers` responds 403 and returns no customer record. → `F-016-T16` 🧪 test-first
23. `[security]` **(T-004)** Given a request that triggers `ForbiddenException` while `ASPNETCORE_ENVIRONMENT=Production`, the 403 body contains `status`, `title` and `requestId` and contains **no** exception type name, message, or stack frame. → `F-016-T08` 🧪 test-first
24. `[security]` **(T-005)** Given an authenticated `GET /api/v1/providers`, the resulting `events` document records the caller's `sub` in an `actor` field **and** still contains no provider email, customer email, or appointment record. → `F-016-T18` 🧪 test-first
25. `[security]` **(T-006)** Given the cache is warm for `{email}` from a request by its owner, when a different authenticated principal requests the same `{email}`, the response is **not** 200-with-appointment-data. → `F-016-T13` 🧪 test-first
26. `[security]` **(T-007)** `POST /api/v1/professions` no longer exists — an authenticated request returns 404/405 and no profession is created by any role; the two profession read routes still return 200 anonymously. → `F-016-T17` 🧪 test-first

> ⚠️ **AC-17 is broadened at Plan.** As written it tests only `GetProvidersQueryHandler`. **All ten** query handlers share the identical publish→query→audit shape and `GetCustomersQuery` serialises every customer record — an equivalent hole. The design and `F-016-T18` cover all ten; AC-17 should be read as applying to all ten (ADR-028).
>
> ⚠️ **Requirement 13 is superseded** by ADR-025 (route deleted, not role-gated) and **requirement 18 is reassigned into this feature** by ADR-028 (threat T-001 makes it reachable here). Requirement 14's *approach* is replaced by ADR-022. The requirement text above is left unedited; these ADRs are the current authority.

---

## User Stories

**F-016-US-01: An anonymous caller cannot read anyone's client list**
*Acceptance criteria: 8*
Given no access token
When a caller requests `GET /api/v1/providers`
Then the response is 401
And no provider record, customer email, or appointment is disclosed

**F-016-US-02: A logged-in customer browsing providers does not receive their appointment books**
*Acceptance criteria: 9*
Given a customer is authenticated and browsing to choose a provider
When they request `GET /api/v1/providers`
Then they receive each provider's public profile and service catalogue
And the payload contains no `AppointmentEntities` and no `SubscribedCustomerCollection`

**F-016-US-03: One provider cannot read another provider's calendar**
*Acceptance criteria: 10*
Given provider A is authenticated with a valid token
When A requests `GET /api/v1/calendar/appointments/{email-of-provider-B}`
Then the response is 403
And when A requests their own email instead, the response is 200 with their appointments

**F-016-US-04: A customer cannot impersonate a provider or edit shared reference data**
*Acceptance criteria: 11, 12*
Given a caller authenticated with only the `Customer` role
When they `POST /api/v1/providers` for an arbitrary email, or `POST /api/v1/professions`
Then both are rejected with 403
And no provider record and no profession is created

**F-016-US-05: A forgotten try/catch can no longer leak a 500**
*Acceptance criteria: 13, 14*
Given an endpoint that raises `ForbiddenException` with no local `try/catch`
When a caller triggers the forbidden condition
Then the response is 403 with a ProblemDetails body
And the 8 endpoints that already catch it locally still return exactly one 403

**F-016-US-06: List endpoints cannot be used to dump the dataset**
*Acceptance criteria: 15, 16*
Given an authenticated caller
When they request `GET /api/v1/providers` with no pagination parameters, and again with an oversized page size
Then both return a bounded page with total-count metadata, capped at the documented maximum
And the response shape is recorded as an ADR in `DECISIONS.md`

**F-016-US-07: Reading data no longer copies it into the audit log**
*Acceptance criteria: 17*
Given the `events` collection is empty
When an authenticated caller requests `GET /api/v1/providers`
Then an audit event is recorded with the operation, status and timestamp
And that event contains no provider email, no customer email and no appointment record

**F-016-US-08: The maintainer can prove an endpoint's authorization, not just its guard function**
*Acceptance criteria: 1, 2, 3, 4, 5, 6, 7*
Given the integration harness
When the suite runs
Then each authorization rule above is asserted against a real route over real HTTP against a throwaway MongoDB container
And the harness refuses to run at all if pointed at a non-container database
And no private key material is ever written to disk

**F-016-US-09: Nothing regressed**
*Acceptance criteria: 18, 19*
Given the feature is complete
When the full suite runs
Then 305+ backend tests pass with 0 failures and 0 warnings, and mobile reports 74
And the anonymous profession reference routes still return 200
And no pre-existing test was deleted or skipped

---

## Testing Approach: Test-Driven Development (TDD)

**Tests are written first.** During Construction (`/build`), for **every acceptance criterion above**, a **failing test is written and run before any implementation code** — the Red → Green → Refactor cycle:

1. **Red** — write the smallest failing test that pins the acceptance criterion, named with the Given/When/Then language from the matching user story. Run it; confirm it fails for the right reason (logic not implemented — not a syntax/import error).
2. **Green** — write the minimum implementation that makes the test pass. Run the test and the full suite; no regressions.
3. **Refactor** — clean up without changing behavior; suite stays green.

The build loop enforces this at a mandatory **TDD gate** (build Step 9a-bis): implementation code for a criterion may not be written until a failing test for it exists. The only exceptions are pure scaffolding, config-only, and infrastructure-only work — and even those require an **explicit human TDD override**. There is no silent skip. (TDD can be disabled only by editing `CONSTITUTION.md` § Test Gates — the Constitution always wins.)

**Security acceptance criteria are enforced mechanically (issue #55).** Any `[security]`-tagged criterion above (threat-derived, materialized on its task via `tasks.cjs ac add`) is not just governed by the prose gate: `node scripts/tasks.cjs done` **structurally refuses** to close a task whose `[security]` AC has no linked test. Name each security test after its threat id (`test_TNNN_…`) and link it with `tasks.cjs ac link-test`. This makes it impossible to close a threat mitigation on a citation alone — the failure mode where a mitigation lived as a task-body reference while its acceptance criterion was never written, and TDD had nothing to bite on.

**Test layers** for this feature (which of the CONSTITUTION §7 gates apply): **Unit** ✅ · **Integration** ✅ — *this feature is the one that first makes the integration layer exist; CONSTITUTION §5's "all integration tests pass" has been unsatisfiable because there were none* · **Security scan** ✅ (§7 marks it always-required and un-uncheckable; it remains unimplemented and is owned by F-017 — see Known Risks) · E2E, Performance, Accessibility, Visual Regression: not applicable.

> ⚠️ **Note for the Plan gate.** CONSTITUTION §7 currently has Integration **unchecked**. This feature builds the integration layer but does **not** amend §7 to require it — that amendment is gated on 10 consecutive green integration runs, tracked separately (inherited from F-018's task T04). Do not silently check the box.

---

## Non-Functional Requirements

- **The harness must not be able to touch real data.** Requirement 5's fail-closed guard is a hard safety property, not a convenience. The repository is **public** and a valid Atlas credential remains recoverable from git history, so an integration suite that resolves a non-container connection string is a live hazard.
- **No private key material on disk, ever** — session keypair in memory only (requirement 3).
- **Integration suite runtime must stay bounded.** Container startup is a measured 4.45 s warm; container-per-class is the design consequence. The suite should stay within a duration the maintainer will actually tolerate locally on a 2-CPU / 4.1 GB VM, and that budget should be enforced rather than hoped for.
- **Pagination caps are a security control, not ergonomics.** An uncapped page size restores the dump the feature exists to remove.
- **Error responses must not become an information oracle.** `GET /api/v1/customers/{email}` currently distinguishes 200 from 404 anonymously, which enumerates emails. Once authenticated this is far less severe, but the 403-vs-404 choice for non-owned resources should be made deliberately.
- **PII must not reach telemetry.** `PiiRedactingProcessor` already strips email patterns from span attributes because `url.path` was exporting real addresses. These routes carry `{email}` in the path; the processor MUST remain in place and MUST cover any new route shape introduced here.
- **No new package without discussion** (CONSTITUTION §9). This feature needs Testcontainers and an ASP.NET testing package; both were approved in F-018's ADR-015 five-package set and should be cited rather than re-litigated.

---

## Out of Scope

- **Everything in F-021 `identity-hardening`** — the account-destroying `RefreshAsync` delete-then-insert, `UseHttpsRedirection` ordering, rate limiting and lockout, and `AssertOwner`'s null-claim hole. Split out because F-016 plus the harness already exceeds one PRD.
- **Token revocation / `jti` denylist** (F-023) — an access token still stays valid up to 60 minutes after logout. Needs a design decision on denylist storage, not a task.
- **Password reset / change / forced-reset** (F-022) — genuinely downstream: delivery needs `NotificationService`, which F-014 wires.
- **Data-subject rights and retention for the accumulated `events` collection** (F-024). Requirement 16 stops the bleeding; it does not clean up what already accumulated.
- **The API gateway and the mobile contract** (F-015) — this feature deliberately changes the contract *before* the client is written against it.
- **Wiring the six unreachable services** (F-014), including the double-booking rule absorbed into it.
- **The rest of F-018** — OpenAPI generation, spec-drift CI, `.editorconfig`, constitution amendments, mobile-CI confirmation, and the Tier 1–3 test sweep across all seven services. Only the six harness tasks come forward.
- **Restructuring `ProviderEntity`'s embedded documents.** Requirement 10 projects at the read boundary; changing the stored shape is a migration and belongs to the refactor program.
- **HTTPS listeners, HSTS, CORS** — no service has an HTTPS endpoint at all today. Real transport hardening is F-017/F-021 territory.

---

## Known Risks

- **The harness sits on the critical path of a live security fix.** If it slips, the exposure stays open longer. *Mitigating:* both of F-018's gating spikes already passed — Testcontainers works on Rancher with zero configuration, and the design is already reshaped around the measured 4.45 s startup. The residual risk is execution, not feasibility. Accepted deliberately: shipping the authz changes unverified was judged the worse trade, since unverifiable authz is what produced the Calendar IDOR.
- **F-021's rate limiter can break this feature's harness.** The harness authenticates repeatedly against `POST /api/v1/auth/login`. F-021 MUST ship its limiter with a test-environment escape. Recorded in both features so neither can forget.
- **The central 403 mapping will be written twice.** It touches the error pipeline in all seven `Program.cs` files, and F-019/F-020 rewrite exactly those files. Accepted: leaving 403s hand-written at 8 call sites until after a three-stage refactor is worse.
- **Rancher resource pressure is the least-tested assumption.** 2 CPUs / 4.1 GB already running k8s, with container-per-class across a growing number of test classes. If it thrashes, the mitigation is fewer, larger test classes — not abandoning containers.
- **CI verification needs a maintainer-pushed throwaway branch.** `main` is PR-protected and CI is path-filtered, so any CI-dependent criterion cannot be verified locally. F-018 hit exactly this and its readiness party logged it as `dependency-missed`: the task graph cannot express "waits on a human." Plan accordingly rather than scheduling those as ordinary tasks.
- **CONSTITUTION §7's security-scan gate is still unimplemented** and is marked always-required and un-uncheckable. It was run by hand at the v0.1.0 ship, which does not discharge it. Owned by F-017. This feature proceeds with that gate unmet, consistent with the guardrail already logged on 2026-08-18.
- **The Atlas credential remains unrotated** — still valid, still recoverable from the public repo's history. Human-only, outside this feature (`ISSUE-002`), but it is what makes requirement 5's fail-closed guard load-bearing rather than pedantic.

---

## Standards Alignment

**Not assessed — the plugin's sources are unreachable at both gates.**

| Gate | Mode | Tier | Outcome |
|---|---|---|---|
| Define Step 6.5 | `--ideate` | advisory | Light skip — logged to STATE.md's Guardrail Log |
| Plan Step 17.5 | `--design` | **enforcing** | **Skip-with-notice — the skill is unavailable, not user-elected** |

The `nordstrom-standards-readiness` plugin **is installed** (`claude plugin list` confirms it), but its six source standards repos do not resolve under the current `gh` auth — needs SSO or VPN — and no local `.nordstrom-standards/` checkout exists. Re-verified at Plan: `gh` could not be exercised in this environment either.

**This is a skip-with-notice, not an `/override`.** The enforcing tier requires an override ADR only when a *human elects* to bypass a working gate; here the gate cannot run at all, and the skill's own instruction for that case is to skip-with-notice and continue. No MUST-level findings were surfaced because none could be computed — **absence of findings here is not evidence of compliance.**

Same condition was recorded at F-013 and F-018. Fixing it needs SSO/VPN access, not a code change.

---

## Readiness Assessment

**Triage:** Full (19 tasks · 7 waves · 3 domains — **20 tasks / 8 waves** after the gate absorbed `T20`) · **Convened:** 2026-08-18 · **Lead:** Atlas, co-chaired with Neo
**Participants:** Atlas, Echo, Neo, Phantom, Jarvis — 5 agents. Muse not required (Step 10.6 triaged Skip, so there are no fix-now UX findings to confirm landed).
**Spawn mode:** `solo` — standing no-Agent-tool instruction. **Advisory only; it did not block approval.**

### Overall: **Fair** — 4 gaps found: **2 resolved before Construction** (1 corrected in-party, 1 resolved at the Step 18 gate), 2 carried forward

| Dimension | Rating | Evidence |
|---|---|---|
| **Completeness** | **Fair** | All 12 PRD sections populated and specific. Out-of-scope list names 9 exclusions, each with a reason and an owning feature. **But:** requirement 8 ("every authz change covered by an integration test") and requirement 20 ("no success-path semantics change") have **no dedicated AC** — both are meta-requirements whose coverage is distributed across AC-8…AC-18 and AC-19 respectively. Defensible, and flagged rather than silently accepted. Standards alignment could not be assessed at either gate (sources unreachable) — **absence of findings is not evidence of compliance.** |
| **Traceability** | **Fair** | 26 ACs → 19 tasks, **no orphan task** and **no uncovered AC** after the correction below. Requirements 1–20 map to ACs; 13 is superseded by ADR-025 and 18 is reassigned in by ADR-028. **🔴 Echo found one real defect — see below.** |
| **Durability** | **Fair** | Dependency graph verified acyclic by `tasks.cjs check`; 7 waves; critical path 9 deep; two bottlenecks (`T02`, `T06`) identified and named in the plan. Wave-6 fan-out is wide, so wall-clock is dominated by the harness chain rather than endpoint work. **But** the feature's central claim — "we can demonstrate it" — has **no CI enforcement** (gap 1 below). |

### 🔴 Defect caught and corrected during the party

**AC-12 contradicted ADR-025.** As written it required *"`POST /api/v1/professions` returns 403 for a caller who does not hold the required role"* — on a route that ADR-025 **deletes**. Left in place it would have sent Build to implement a role check against a route that no longer exists, and the correct behaviour (404/405) would have read as a test failure.

Found by Echo cross-checking the AC list against the ADR registry rather than against the requirements alone. **Struck and replaced by AC-26** (`[security]`, T-007). This is exactly the class of drift the readiness party exists to catch: the design gate changed a decision, the requirement text was correctly annotated, and the *acceptance criterion* was missed.

### Phantom's issue-#55 check — ✅ passes

All **seven** mitigate-now threats are materialized as `[security]`-tagged ACs on tasks, verified against the raw task records (e.g. `F-016-T06` carries `AC1|security|T-002|…`) and confirmed by `tasks.cjs check` reporting exactly 7 `security-ac-untested` findings for F-016. **No `security-ac-unmaterialized` and no `design-finding-unlinked` gaps.** *(An initial read of `ac list --json` appeared to show untagged ACs; that was a projection artifact of the JSON output shape, not a real gap — confirmed against the task files.)*

### Open gaps carried into Construction

1. **✅ RESOLVED at the Step 18 gate — `F-016-T20` absorbs F-018's `T18`.** The gap: the integration suite had no CI enforcement, so a feature whose thesis is "authorization you can prove" left the proof running on one laptop with nothing noticing if it stopped. The maintainer chose to absorb the CI job rather than accept local-only. **Eight F-018 tasks are now absorbed, not six.** ⚠️ Residual: `T20` cannot be verified locally (PR-protected `main`, path-filtered pipeline) and needs a maintainer-pushed throwaway branch — the `dependency-missed` class the task graph cannot express. `T04` (10-green-run counter) remains unabsorbed, so CONSTITUTION §7's Integration box stays unchecked.
2. **`tasks.cjs ready` is not feature-scoped.** It returns paused-F-018 tasks alongside F-016's, so the Build loop can start `F-018-T02`. Filter on `epic:secure-public-endpoints`.
3. **Requirements 8 and 20 have no dedicated AC** (distributed coverage). Low severity; recorded so no one later reads it as an omission.

### Not gaps, but the party wanted them visible

- CONSTITUTION §7 leaves Integration unchecked and this feature deliberately does not tick it — gated on 10 consecutive green runs, tracked separately (F-018's T04, not absorbed).
- The Rancher VM (2 CPUs / 4.1 GB, already running k8s) is the least-tested assumption in the plan.
- `CacheAside` has no test and returns `default!` on a 500 ms timeout, so `T13`'s assertion must be "not 200-with-data" rather than "exactly 403".

---

## Design Docs

- Architecture: [`ARCHITECTURE.md`](../design/secure-public-endpoints/ARCHITECTURE.md)
- Data model: [`data-model.md`](../design/secure-public-endpoints/data-model.md)
- API contracts: [`api-contracts.md`](../design/secure-public-endpoints/api-contracts.md) — **contains the paginated response contract F-015 consumes (AC-16)**
- Threat model: [`threat-model.md`](../design/secure-public-endpoints/threat-model.md) — triage **Full** (3/3): 8 threats, 1 CRITICAL / 2 HIGH / 5 MEDIUM. MOM: [`MOM_threat-model_secure-public-endpoints_2026-08-18.md`](../mom/MOM_threat-model_secure-public-endpoints_2026-08-18.md)
- UX review: [`ux-review.md`](../design/secure-public-endpoints/ux-review.md) — triage **Skip** (0/3), no UI surface; two downstream consequences recorded as inputs to F-015

### ⚠️ Design changed this PRD in two places

Recorded here so the PRD does not read as authoritative where the design superseded it:

1. **Requirement 18 is reassigned into this feature.** The PRD deferred `AssertOwner`'s null-claim fix to F-021 ("SHOULD be left to F-021"). Threat **T-001** establishes that the response-shape projection in requirement 10 reuses `AssertOwner`, whose null-claim pass (`string.Equals(null, null)` is `true`) then lands on the **owner** branch and returns the unprojected entity. The hole is unreachable at these routes today; **this feature makes it reachable.** It must be fixed here.
2. **Requirement 14 cannot be met as scoped.** The existing exception handler is registered *inside* `if (app.Environment.IsDevelopment())` in all seven services, so a `ForbiddenException` mapping added there would produce 403 in Development and a bare 500 in Production. ARCHITECTURE **AD-1** replaces the approach with a shared `IExceptionHandler` registered unconditionally — a strictly larger change that also fixes a latent production defect, and an ADR candidate.

Two further items are **scope additions the design proposes and the human must decide** (threat model open questions 1–3): role-scoping `GET /api/v1/customers` (**T-003**), the disposition of `POST /api/v1/professions` given that no admin role exists (**T-007**), and adding an `actor` field to `Event` (**T-005**, which would cost this feature its clean no-migration rollback).

---

## Related Episodes

- [EPISODE_aspire-wiring_2026-08-17.md](../episodes/EPISODE_aspire-wiring_2026-08-17.md) — F-013 (`v0.1.0`). Established `MongoConnectionResolver`, the shared `IMongoClient` singleton, and `PiiRedactingProcessor`. Directly relevant: F-013 discovered that enabling telemetry exported real customer emails via `url.path`, which is why this feature's NFRs treat the redaction processor as load-bearing. F-013 also demonstrated the failure mode this PRD guards against — a threat "mitigated" by reasoning rather than observation.
- F-018 `api-refactor-foundations` — Inception complete, Construction aborted before any code (PR #37). Its PRD, architecture, threat model and both passed spikes are the source of this feature's section A. Not an episode; not shipped.

---

## Approval

**Approved by:** ogdevlabs
**Date approved:** 2026-08-18
**Notes:** Approved 2026-08-18T18:50:52Z. The flagged product call — **authenticating provider discovery** — was put to the maintainer explicitly with its supporting evidence (F-003 `customer-onboarding-flow`, status Shipped, defines its flow as *"a customer signs up, discovers providers, and subscribes to one"*, making discovery post-signup by the product's own definition) and approved. Requirement 9 therefore stands as written. Requirement 10's projection of the embedded appointment book applies **regardless**, so an authenticated non-owner still cannot read another provider's client list.

One correction was applied before approval: the anonymous PII GET count is **five**, not four — `GET /api/v1/services/{email}` had been omitted from the Discover summary. `GET /api/v1/professions*` remains the sixth anonymous route and stays anonymous by design.
