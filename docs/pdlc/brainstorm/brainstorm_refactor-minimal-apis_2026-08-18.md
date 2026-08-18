---
feature: refactor-minimal-apis
date: 2026-08-18
status: prd-approved
last-updated: 2026-08-18T16:10:00Z
approved-by: ogdevlabs
approved-date: 2026-08-18T15:25:00Z
prd: docs/pdlc/prds/PRD_F-018_api-refactor-foundations_2026-08-18.md
---

# Brainstorm Log: API Refactor Program (F-018 → F-019 → F-020)

**Originally opened as:** F-018 `refactor-minimal-apis`
**Now the program-level record.** Discover established the requested scope was too large for one PRD; it was decomposed into three features on 2026-08-18. **This log is the shared research for all three** — F-019's and F-020's own brainstorms should reference it rather than re-deriving the reference pattern or the defect list.
**Feature being brainstormed here:** F-018 `api-refactor-foundations` (stage 1 of 3)
**Reference implementation named by the user:** https://github.com/Gramli/AuthApi

---

## Decomposition (decided 2026-08-18)

The full Clean Architecture target is **preserved**, not reduced — only staged, so the integration-test harness exists *before* the endpoint rewrite instead of being built alongside it.

| Feature | Slug | Scope | Depends on |
|---|---|---|---|
| **F-018** | `api-refactor-foundations` | Testcontainers integration harness in CI · `MobileApp` into CI (`agenda-buddy-prr`) · `Persitency` → `Persistence` · constitution amendments + ADRs. **No endpoint changes.** | — |
| **F-019** | `api-refactor-pilot-booking` | Full Clean Architecture + all 5 packages applied to `Booking` only, proving the shape end-to-end | F-018 |
| **F-020** | `api-refactor-rollout` | Roll the proven shape across the remaining 6 services; delete the 6 `RequestCollection` classes and 6 duplicated exception blocks | F-019 |

**Why the harness comes first.** Episode 001 concluded that both of F-013's real defects were invisible to review and surfaced only by *running* the software, and that "verify the acceptance criteria" must mean run the thing. A rewrite of every endpoint with only unit tests underneath repeats that mistake at larger scale.

**Cross-feature scope change:** the integration-test capability moved **out of F-017** and into F-018, and is now Testcontainers-based rather than bare `WebApplicationFactory`.

### Conflicts found in Round 1 and how they were resolved

| # | Conflict | Resolution |
|---|---|---|
| ① | The user asked to **keep CQRS via MediatR** *and* to adopt **`SmallApiToolkit`**, whose `IHttpRequestHandler<TResponse,TRequest>` is itself an endpoint→handler dispatcher. The reference uses it *because* it rejected MediatR. Adopting both = two competing dispatch abstractions. | **MediatR is the single dispatcher.** Endpoints call `mediator.Send(command)` — which finally honours CONSTITUTION §3 and removes the hand-constructed `new SomeCommandHandler(...)` calls (defect #3). `SmallApiToolkit` is taken only for `DataResponse<T>`, the validation base class, and `ExceptionMiddleware`. `IHttpRequestHandler` is deliberately **not** used. |
| ② | **`Validot` replaces `MiniValidator`, which CONSTITUTION §4 mandates.** Plus §9 needs amending for the five packages and for retiring the `Persitency`-rename prohibition. | **Amend the constitution with one ADR per substantive change.** §1 (still says .NET 8 — it is net10), §4 (MiniValidator → Validot), §9 (five packages approved; stale rename prohibition removed now that its stated condition — "a dedicated refactor is planned" — is met). Owned by F-018. |
| ③ | **MAUI in CI needs more than a host install** — I raised this, and **I was wrong.** | **Withdrawn after reading `.github/workflows/dotnet.yml`.** CI already has three dedicated mobile jobs: `build-android` (installs `maui-android`), `build-ios` (**already on `macos-latest`**, installs `maui-ios`), and `build-mobile-tests`. The workload installs and the macOS runner exist. Nothing to add. |

### Correction — the `MobileApp` premise was stale in both directions

STATE.md's outstanding list said `agenda-buddy-prr` was still open; the bead is in fact **closed**, and `MobileApp.Tests` passes **67 (7 skipped, 74 total)**, verified by running it locally on 2026-08-18. I had propagated the stale claim into CLAUDE.md, OVERVIEW.md and `docs/pdlc/context/11-testing.md` earlier in this session; all three are corrected, along with a comment in `.github/workflows/dotnet.yml` that still asserted MobileApp does not compile.

**Two consequences:**
1. **F-018's scope item 2 shrank.** "Add MobileApp to CI" is largely already true. What remains is confirming the three path-filtered mobile jobs pass on a real run, and settling whether the project's headline test count is **379** (305 backend + 74 mobile) rather than 305.
2. **MobileApp's exclusion from `agenda-buddy-backend.slnf` is by design**, not a workaround for a broken project — it keeps a mobile-only break from failing the backend job.

## Divergent Ideation
_Not run._

## Socratic Discovery

**Interaction mode:** Sketch
**Round 1 completed:** 2026-08-18T14:00:00Z

### Round 1 — Problem Statement

**Q1: What problem does this specific feature solve?**
**A:** *(drafted, accepted)* The affected user is a developer, not a provider. Four costs, each evidenced in source:

1. **Success cannot be told from failure reliably.** Control flow depends on `!eventResponse.ToLower().StartsWith("exception")` (`Booking/Program.cs`). A handler returning a message that happens to start with "exception" reads as failure; an error message that doesn't reads as success. An empty string also reads as failure.
2. **Nothing is cancellable.** Every handler is passed `new CancellationToken()` (`Booking/Requests/RequestCollection.cs`), so client disconnects never propagate and work continues for requests nobody awaits.
3. **The persistence model is the public API contract.** `AppointmentEntity` is both request body and response body. This is the mechanism behind F-016's PII exposure — `GET /api/v1/providers` returns embedded appointments carrying customer emails because the entity *is* the response.
4. **Every endpoint repeats the same four blocks** (validate → ownership try/catch → call → string-sniff), so any fix is applied 7× and a forgotten `catch` turns a 403 into a 500.

**Q2: Who specifically will use this feature?**
**A:** *(drafted, accepted)* Only this repo's developers — currently a single maintainer working with AI agents. No provider- or customer-facing behaviour changes. The real consumers are **F-014, F-015 and F-016**, all of which must touch this same endpoint layer.

**Q3: What does success look like?**
**A:** *(user chose "Both")* Two tiers.

*Gate this feature on mechanical, grep-provable counts:*
- 0 occurrences of string-based control flow (no `StartsWith("exception")` style checks)
- 0 occurrences of `new CancellationToken()`
- 0 persistence entities appearing in any route signature
- All existing tests still pass, with **no test deleted**

*Record as a prediction to reconcile at F-014's ship:* F-014 can add a route for one previously-unreachable capability by touching only one new file, with no change to any `Program.cs`. This gets reconciled the way `METRICS.md` reconciles readiness — prediction now, verdict later.

**Q4: What are the technical constraints or dependencies?**
**A:** *(drafted, then substantially amended by the user)*

Carried over as drafted:
- CONSTITUTION §3: the EventStore audit on every command must stay ("do not remove this pattern").
- CONSTITUTION §3: the cache-aside pattern must not be bypassed.
- `MobileApp` currently does not compile in CI, so mobile is unprotected while routes move.

**User amendments — these change the feature materially:**

| # | Decision | Consequence |
|---|---|---|
| A | **Keep CQRS via MediatR.** | CONSTITUTION §3 is *preserved*, not overridden. But the reference explicitly does CQRS *without* MediatR, so we take its structure and keep MediatR as the dispatcher. In practice this means fixing defect #3 by making MediatR **actually dispatch** (`mediator.Send(command)`) instead of hand-constructing handlers. **Conflicts with decision B — see Open Conflicts.** |
| B | **Adopt all five packages** (FluentResults, Validot, Mapster, GuardClauses, SmallApiToolkit). | CONSTITUTION §9 requires discussion before adding packages; this is that discussion, and the answer is yes. **`SmallApiToolkit`'s `IHttpRequestHandler` is a dispatch abstraction that competes with MediatR — see Open Conflicts.** |
| C | **Add integration tests using Testcontainers.** | New scope not in the original feature description. **Overlaps F-017**, which currently owns "build an integration-test capability (WebApplicationFactory)". |
| D | **Fix the `Persitency` typo.** | CONSTITUTION §9 forbids this "until a dedicated refactor is planned". This *is* that refactor, so the condition is now satisfied — but §9's text should be amended so the rule doesn't outlive its reason. Touches every consumer of `EventAndCommands.Persitency`. |
| E | **Include `MobileApp` in CI; MAUI is installed on the host.** | Resolves `agenda-buddy-prr`. **Caveat: "installed on the host" is this dev machine. GitHub Actions runners need their own `dotnet workload install maui`** — and iOS builds need a macOS runner. See Open Conflicts. |
| F | **Full Clean Architecture per service** (Api/Core/Domain/Infrastructure × 7). | Up to 28 projects, against a codebase that currently has no integration tests. Drives the decomposition question below. |

### Round 2 — Future State / Key Capabilities (F-018 foundations only)

**Round 2 completed:** 2026-08-18T14:20:00Z

**Q5: What must an integration test assert? (This defines the harness.)**
**A:** *(drafted, accepted)* All three tiers:
1. **Route contract** — a real HTTP request through the real pipeline returns the expected status and body shape. Nothing today can catch F-019 accidentally changing a route or status code.
2. **Persistence round-trip** — POST then GET returns the written data against a **real MongoDB**, exercising `[BsonElement]` snake_case mappings and embedded-document topology. Mocked repositories cannot see a wrong BSON mapping.
3. **The EventStore audit actually fires** — CONSTITUTION §3 mandates an audit event per command and says "do not remove this pattern", yet **no test asserts an event was written**. Without tier 3, F-019/F-020 could silently drop the audit trail and CI would stay green. This is the highest-value tier.

**Q6: Where does the harness live, and which containers does it start?**
**A:** *(user overrode the recommendation)* One shared project, **`AgendaBuddy.IntegrationTests`**, with a `WebApplicationFactory` per service under test and Testcontainers fixtures — MongoDB always, Kafka only for the provider-registration path (the sole place a topic is created). **Container per test**, for maximum isolation and no cross-test state.

> ⚠️ **Recorded risk (raised, and the user chose this deliberately).** I recommended reusing one container per test *class*. Container-per-test was chosen instead. A MongoDB container costs roughly 1–3 s to start, and the local Rancher VM is **2 CPUs / 4.1 GB already running a k8s cluster** (DEPLOYMENTS.md gotchas). At a few dozen integration tests this is minutes of wall clock locally, and xUnit parallelism will contend for those 2 CPUs. Mitigations to weigh at Design: Testcontainers' reuse flag, tuning the xUnit parallel collection settings, or scoping container-per-test to the mutation tests only. **Flagged for Adversarial Review to pressure-test — not silently accepted.**

**Q7: When do integration tests run?**
**A:** *(drafted, accepted)* On every PR, as a **separate CI job** from the backend unit job, so container flakiness is never mistaken for a unit failure. GitHub `ubuntu-latest` runners ship Docker, so Testcontainers needs no extra setup. Add to CONSTITUTION §7 as a required gate **only once it is green and stable** — declaring a gate before it can pass is exactly how §7's security-scan gate came to be mandated-but-unimplemented, which we had to ship around at v0.1.0.

**Q8: `Persitency` → `Persistence` mechanics?**
**A:** *(drafted, accepted)* Do it in F-018 and amend §9.

⚠️ **CONSTITUTION §9 overstated this risk.** It claims renaming "breaks existing references across all consumers". Measured on 2026-08-18: **11 files, one reference each, and zero coupling in any `.json`, `.yml`, `.csproj` or `.slnf`** — so no configuration or collection name depends on the spelling.

| Location | Count |
|---|---|
| `EventAndCommands/Persitency/{Event,EventStore,IEventStore}.cs` (the namespace declarations) | 3 |
| `EventAndCommands/{GlobalUsings,ServiceCollectionExtensions}.cs` | 2 |
| `GlobalUsings.cs` in Booking, Calendar, Customer, Profession, Provider, Services | 6 |

It is a directory rename, a namespace rename, and 7 one-line edits. Land it as its own commit for easy review and revert, and rewrite §9's prohibition so the stale rule does not outlive its reason. *(Identity does not reference EventStore at all.)*

### Round 3 — Acceptance Criteria

**Round 3 completed:** 2026-08-18T14:30:00Z — **Socratic Discovery complete.**

**Q9: What is the pass/fail bar for the harness itself?**
**A:** *(drafted, accepted)* The harness is done when it **proves it can catch the three failure classes**, not when it merely exists: at least one passing integration test per tier for each service with write endpoints (Booking, Customer, Provider, Services, Profession), plus route-contract coverage for the read-only ones (Calendar, Identity). All 379 existing tests still pass.

**Q10: How do we prove the audit-trail test works rather than just passes?**
**A:** *(drafted, accepted)* A **mutation check**: temporarily remove the EventStore write, confirm the tier-3 test goes **red**, restore it, and record that as evidence in the verification doc.

This is a direct application of episode 001's central lesson. Threat T-004 was recorded as mitigated because instrumentation *should* record route templates; when the test was finally written it **failed** — `url.path` was exporting real customer email addresses. A §3-invariant claim asserted by reasoning deserves the same red-then-green proof.

**Q11: Acceptable CI wall-clock budget for the integration job?**
**A:** *(user input — no context to draft from)* **Under 10 minutes.** This is the objective trigger for revisiting the container-per-test decision from Q6: if the job exceeds 10 minutes, move to per-class container reuse or Testcontainers' reuse flag. The decision then gets made on a measurement rather than on preference. *(For reference, the backend unit job currently finishes in well under a minute.)*

**Q12: Which ADRs does F-018 produce?**
**A:** *(drafted, accepted)* Four, continuing from ADR-013:

| ADR | Subject |
|---|---|
| **ADR-014** | API refactor program — adopt the Gramli/AuthApi shape, staged F-018 → F-019 → F-020, **MediatR retained as the single dispatcher**, `SmallApiToolkit`'s `IHttpRequestHandler` explicitly rejected to avoid two competing dispatchers |
| **ADR-015** | The five packages (FluentResults, Validot, Mapster, GuardClauses, SmallApiToolkit) — §9 amendment, recording the caveat that SmallApiToolkit's own README scopes it to "small-scale or example web APIs" |
| **ADR-016** | Validot replaces MiniValidator — §4 amendment |
| **ADR-017** | Testcontainers integration harness, container-per-test, with the 10-minute revisit trigger; moved out of F-017 |

Plus two non-ADR corrections: §1 (still says .NET 8 — the project is `net10.0`) and the removal of §9's `Persitency` rename prohibition, whose stated condition is now met.

## Progressive Thinking (Agent Team Meeting)

**MOM:** [`docs/pdlc/mom/api-refactor-foundations_progressive-thinking_mom_2026_08_18.md`](../mom/api-refactor-foundations_progressive-thinking_mom_2026_08_18.md)
**Completed:** 2026-08-18T14:45:00Z · Run inline by the lead (subagents not spawned — not requested this session)

### Confirmed Facts

- **379 tests** (305 backend / 12 projects + 74 mobile), **zero integration tests**, and **no test asserts an EventStore audit write** — the §3 invariant is entirely unguarded.
- `AddAgendaBuddyAuthentication()` **throws `ApplicationException` when `JWT_PUBLIC_KEY` is unset** (`Library.ServerAuth/AuthenticationExtensions.cs:18-22`). Tokens must be RS256, issuer `agenda-buddy-identity`, unexpired, `ClockSkew = TimeSpan.Zero`.
- **No service `.csproj` has `InternalsVisibleTo`** — only `MobileApp`, `AgendaBuddy.AppHost` and `Kafka` do. Services use top-level statements, so `Program` is internal.
- `MediatR` is registered and injected in all 7 services but **`mediator.Send` is never called**; zero `INotificationHandler` implementations exist.
- **No committed OpenAPI spec**; Swagger runs only in `Development`.
- CI already covers mobile via three jobs including an iOS build on `macos-latest`. `ubuntu-latest` ships Docker.
- Local dev is Rancher Desktop: **2 CPUs / 4.1 GB already running k8s**, `docker` **not on PATH**.

### Accepted Inferences

- Each service under integration test needs `<InternalsVisibleTo Include="AgendaBuddy.IntegrationTests" />`.
- The harness needs an **RSA keypair generated per test session** — the public key to boot the host, the private key to mint valid tokens.
- **Testcontainers probably will not work locally out of the box** — Rancher's socket is `~/.rd/docker.sock`, not the Docker Desktop default, so `DOCKER_HOST` or `.testcontainers.properties` needs configuring. CI passing while local dev fails is the worse failure order.
- Injecting the Mongo connection string is a single config override; no production code change.

> **Inference withdrawn.** Echo assumed OTLP export would need suppressing in tests. It is already conditional on `OTEL_EXPORTER_OTLP_ENDPOINT` (`AgendaBuddy.ServiceDefaults/Extensions.cs:115-117`), which is unset in a test host — telemetry is naturally inert. **One fewer prerequisite than assumed.**

### Key Consequences

1. **Exactly three hard prerequisites, and they are the critical path.** Until `InternalsVisibleTo`, the keypair, and the connection-string override all work, `WebApplicationFactory` throws and **nothing runs**. A spike proving one service boots must precede everything else.
2. **A shared per-session fixture is unavoidable** despite container-per-test — keypair and config wiring are session-scoped; only the *container* is per-test. Design must keep those lifetimes distinct.
3. **Kafka is the wall-clock threat, not Mongo** (~5–10 s vs ~1–3 s). Only provider registration touches it; confine it to as few tests as possible or the 10-minute budget is at risk from one path.
4. CONSTITUTION §5's "all integration tests pass" becomes satisfiable for the first time.

### Risks & Unknowns

- ⚠️ **Phantom:** minting tokens needs a private key. A committed fixed test keypair would be a **new secret-shaped artifact three weeks after a credential incident that is still unremediated**, and would trip F-017's future scanner. Generate in memory per session; never write to disk.
- **Echo:** container-per-test with xUnit parallelism on 2 CPUs risks timeout flakes indistinguishable from real failures — *"people learn to re-run red builds."*
- **Pulse:** Testcontainers pulls images on first run; the NuGet cache does not cover Docker images.
- **Bolt:** `Calendar` and `Identity` have no write endpoints, so tiers 2 and 3 do not apply — the per-service matrix is not uniform.

### Conflicts Resolved

| Conflict | Resolution |
|---|---|
| **A — Echo vs the user's container-per-test choice.** Echo wanted per-class reuse. | **User's decision stands, not overridden.** The 10-minute CI budget (Q11) is the objective trigger to revisit. Echo's concern recorded as a risk rather than dropped. |
| **B — Neo vs Echo: what should tests assert, given F-019 changes every response envelope?** | **Neo's position, with Echo's floor.** Assert **behaviour that must survive the refactor** — HTTP status, persisted DB state, whether the audit fired — **not** the JSON envelope. Envelope assertions are added *in F-019*, with the change that introduces `DataResponse<T>`. This makes F-018's tests a genuine regression net for F-019 instead of a snapshot of the old shape. |
| **C — Phantom vs Pulse: RSA generation cost.** | Generate **once per test session**, in memory. Satisfies Phantom at negligible cost. |

### User Escalation — resolved

**Question:** Should F-018 commit an OpenAPI spec as the canonical route-contract baseline?
**Answer:** **"Commit it now, accept the churn"** (Jarvis's position).

Consequences carried forward: the spec is an **F-018 deliverable and acceptance criterion**; because Swagger only runs in `Development`, generation needs a deterministic build/CI or test-driven path rather than "start the app and download it"; F-019 and F-020 must treat **an unreviewed spec diff as a defect**; and diffing the spec against `MobileApp`'s expected routes hands **F-015 a concrete artifact** for the mismatch it exists to fix.

### Design Priorities (ranked)

1. **Solve the three prerequisites first** — spike one service booting under `WebApplicationFactory` before any other work.
2. **Verify Testcontainers against Rancher Desktop early** — local breakage while CI passes is the worse order, and `docker`-not-on-PATH is a known trap here.
3. **Tier 3 (audit fired) with the mutation check** — highest value; guards the one §3 invariant F-019/F-020 could silently break.
4. **Assert behaviour, not envelopes** (Conflict B).
5. **Per-session in-memory JWT keys**, never on disk.
6. **Measure against the 10-minute budget before scaling test count**; confine Kafka.

**Safely deferred:** tiers 2–3 for `Calendar`/`Identity` (no write endpoints — route contract only); Docker image pinning (only if the budget is approached); extracting shared abstractions into a common project (an F-020 question).

## Adversarial Review

**Completed:** 2026-08-18T15:00:00Z

### Findings

1. **The success metrics measure F-019, not F-018.** Every Q3 zero-count describes changes made by F-019/F-020. F-018 touches no endpoints, so it had **no success metric of its own** beyond "the harness exists". → *addressed, see follow-up 1.*
2. **"One test per tier per write-service" is coverage theatre.** It proves the harness *can* assert, not that endpoints are protected. F-019 rewrites Booking's three endpoints; one test cannot catch a regression in the other two. → *accepted knowingly, see follow-up 1.*
3. **Container-per-test and the 10-minute budget may be arithmetically incompatible, and nobody did the arithmetic.** At the AC minimum (~17 tests × ~2 s) startup is ~35 s. At coverage that would actually protect F-019 (60–100 tests) it is **2–4 minutes of pure container startup** on 2 contended CPUs. The budget is satisfiable only at a thinness that isn't useful; one of the two will quietly give.
4. ⚠️ **`Identity` has FIVE write endpoints — the tier matrix rested on a false fact.** Bolt asserted Identity had none, so it was assigned route-contract only. Verified: `POST /register`, `/login`, `/refresh`, `/logout`, `/device-token` (`Identity/Program.cs:114,134,145,158,170`). **The most security-critical write surface in the system was about to receive the thinnest coverage.** → *fixed, follow-up 2.*
5. ⚠️ **`Identity` is already in the target shape, which undermines the pilot rationale.** It defines real request DTOs (`Identity/Requests/AuthRequests.cs:5` → `public record RegisterRequest(`) and has **no `RequestCollection`** — the other six do. Defects #3 and #7 do not apply to it. Nobody asked whether the **in-repo** example should be the reference rather than an external repo. → *addressed, follow-up 2.*
6. **The mutation check is a one-time ritual that rots.** It proves the test worked once, on one machine. Nothing stops the test being weakened later; there is no mutation-testing tool in the stack. It is evidence, not a guard.
7. **Nobody verified Testcontainers works on Rancher, yet everything depends on it.** Priority #2 and still an *accepted inference*. If it fails locally, developers won't run integration tests and the net exists only in CI — where it was never the mechanism protecting local refactoring.
8. **OpenAPI generation is blocked behind the same critical path, and unsequenced.** Swashbuckle generates from a *running* app and Swagger runs only in `Development`. A test-driven dump needs the three prerequisites first; the build-time alternative (`Microsoft.Extensions.ApiDescription.Server`) is an unmentioned sixth dependency. The decision was taken without a mechanism.
9. **"Add the §7 gate once green and stable" repeats the failure it claims to avoid.** "Stable" had no definition, window, or owner — structurally identical to the §7 security-scan gate that was declared required, never implemented, and shipped around on this very day. → *fixed, follow-up 3.*
10. **F-018 has no rollback story.** It edits 7 production `.csproj` files, renames a namespace, and adds a CI job. If finding 7 kills the harness, those edits are already on `main`.
11. **The cache-aside invariant gets no guard while the audit invariant gets two.** §3 protects both equally ("do not bypass"). F-019/F-020 can break cache-aside just as silently. Asymmetric, with no stated reason.
12. **Conflict B's resolution is softer than it sounds.** "Assert behaviour, not envelope" presumes we know what F-019 preserves. If F-019 changes `Created` → `Ok` with an envelope, the status-code assertions break too — and F-019's design does not exist yet.
13. **The 379 headline includes 7 skipped tests and nobody asked why.** 372 actually execute. Skipped tests inside the regression baseline for a 7-service rewrite are a silent gap. *(Reason not yet determined — carried as an open question.)*
14. **F-014/F-015/F-016 were never sequenced against F-019/F-020.** F-016 adds authorization and pagination to *existing* routes: land it first and the work gets restructured; land it later and the PII exposure stays open through a long refactor. → *fixed, follow-up 3.*

### Follow-up Q&A

**Q1 (findings 1+2+3): F-018 has no metric of its own, and its coverage AC is satisfiable only at a thinness that won't protect F-019. How do we fix the gate?**
**A:** **"Keep one-per-tier, accept it's a smoke test."** The AC stays as drafted, and the PRD must **state plainly** that F-018 delivers a *working harness, not a regression net* — building the actual net is F-019's job.

> This is a deliberate, documented trade, not an oversight. Two consequences to carry forward: **(a)** F-019's plan must include sizing the real coverage against finding 3's arithmetic, because that is where the 10-minute budget will actually be tested; **(b)** finding 1 stands unresolved by design — F-018's honest success criterion is "the harness runs and can assert all three tiers", and the zero-count metrics belong to F-019/F-020.

**Q2 (findings 4+5): Identity has 5 write endpoints and already uses DTOs with no RequestCollection. Does that change anything?**
**A:** **"Fix the matrix; use Identity as the in-repo reference."**
- `Identity` gets **full tier coverage**, not route-contract only — it is the most security-critical write surface in the system.
- `Identity/Requests/AuthRequests.cs` becomes the **in-repo precedent** the new pattern must stay consistent with, so F-019 does not invent a third style alongside the external reference and the existing Identity style.
- **`Booking` remains the pilot** — it is the only service exercising Kafka, the EventStore audit, and `RequestCollection` removal together.
- Revised matrix: full tiers for **Booking, Customer, Provider, Services, Profession, Identity** (6); route-contract only for **Calendar** (verified read-only — 2 GETs at `Calendar/Program.cs:113,141`).

**Q3 (findings 9+14): "stable" is undefined, and F-016 is unsequenced against the rewrite. Your call?**
**A:** **"Define 'stable' concretely; F-016 goes before the rewrite."**
- **"Stable" = 10 consecutive green CI runs of the integration job**, after which §7 is amended. A number and an owner, not a vibe — the specific thing that went wrong with the §7 security-scan gate.
- **F-016 ships before F-019/F-020**, accepting that its authorization and pagination work will later be restructured. Leaving unauthenticated PII exposure (`GET /api/v1/providers` returning customer emails) open across a long refactor is the worse trade.
- **Recorded as a real dependency edge:** `F-019 depends_on [F-018, F-016]`.

### Findings left open (no dedicated follow-up — carried into the PRD's known-risks section)

6, 7, 8, 10, 11, 12, 13. Of these, **7 (Testcontainers on Rancher unverified)** and **8 (no OpenAPI generation mechanism)** are the two most likely to derail F-018 and should become explicit spike tasks at Plan. **10 (no rollback story)** and **13 (7 skipped tests, reason unknown)** are cheap to close and should not be left to Construction.

## External Context

### Reference: Gramli/AuthApi (fetched 2026-08-18)

The user named this repo as the pattern to follow. What it actually does:

**Layering — Clean Architecture, four projects under `src/`:**

| Project | Responsibility |
|---|---|
| `Auth.Api` | Entry point: endpoints, middleware, configuration |
| `Auth.Core` | Business logic — use cases and handlers |
| `Auth.Domain` | Domain models, commands, queries |
| `Auth.Infrastructure` | Database, repositories |

(Plus `Auth.Frontend`, an Angular app, and `Tests/HttpDebugTests` — neither relevant here.)

`Auth.Api` internal folders: `EndpointBuilders/`, `Middlewares/`, `Configuration/`, `BasicAuthentication/`, `Properties/`.

**Patterns:**

1. **`IHttpRequestHandler<TResponse, TRequest>`** — one handler per operation, resolved from DI straight into the route delegate via `[FromServices]`. From `SmallApiToolkit`:
   ```csharp
   MapPost("favorite", async ([FromServices] IHttpRequestHandler<int, Command> handler, ...)
       => await handler.SendAsync(command, cancellationToken))
   ```
2. **`ValidationHttpRequestHandler<TResponse, TRequest>`** — base class taking an `IRequestValidator<TRequest>`, so validation is decoupled from handler bodies instead of repeated in each one.
3. **`DataResponse<T>` / `HttpDataResponse<T>`** — a uniform envelope with `Data` and `Errors`, so every response has the same shape.
4. **CQRS *without* MediatR** — the README is explicit: *"Command-Query separation without the complexity of MediatR"*, achieved by "direct handler injection in Minimal APIs".
5. **`FluentResults`** — *"instead of exceptions for flow control"*.
6. **`Validot`** — declarative, performant validation rules.
7. **`Mapster`** — object-to-object mapping.
8. **`GuardClauses`** — defensive programming.
9. **Route grouping** — no custom grouping construct; native `MapGroup()` plus a `MapVersionGroup()` extension for versioned routes (`someGroup/v1/myGet`).
10. **Middleware** — `LoggingMiddleware`, `ExceptionMiddleware`, CORS helpers.

**Caveat worth recording now, not later:** `SmallApiToolkit` is Gramli's own library, and its README describes it as *"designed for building small-scale or example web APIs"*, with production-readiness applying *"primarily to the core handler pattern."* Adopting the **pattern** is low-risk. Adopting the **package** is a supply-chain and maintenance decision that needs to be made deliberately.

## Current State (grounded in source, read 2026-08-18)
_See Socratic Discovery below — the concrete defects this refactor would address are recorded there._

## Edge Case Analysis

**Completed:** 2026-08-18T15:15:00Z

Mechanical path trace over the F-018 harness concept itself (not the application's endpoints — those belong to F-019/F-020). Handled paths discarded silently.

### Findings

| # | Category | Scenario | Trigger Condition | Addressed? | Risk if Unhandled |
|---|---|---|---|---|---|
| 1 | Integration failure | Docker daemon not running when tests execute | Rancher Desktop not started; must be launched by hand | No | Cryptic Testcontainers error reads as a test bug |
| 2 | Integration failure | Image pull fails — offline or Docker Hub rate-limit | Cold cache, no network, or HTTP 429 | No | CI red for infra reasons, indistinguishable from a real failure |
| 3 | Concurrency | Integration tests run while the Aspire AppHost is already up | Developer forgets to stop the AppHost | No | 2-CPU VM resource exhaustion; confusing timeouts |
| 4 | Partial completion | Test process killed mid-run leaves containers orphaned | Ctrl-C, or CI job timeout | No | VM fills with orphan containers; later runs fail |
| 5 | Partial completion | Mutation check performed, restore forgotten, change committed | Developer mutates the audit write and commits | No | **The audit write is permanently removed — the exact invariant being protected** |
| 6 | Permission boundary | Testing a 401 needs an expired token, but `ValidateLifetime=true` / `ClockSkew=0` | Any auth-failure-path test | No | Expiry and 401 paths untestable without time control |
| 7 | Permission boundary | Testing a 403 needs a token for a different user | Ownership-guard tests | Partial | Keypair exists but no token-factory contract specified |
| 8 | Migration/transition | Tests written against `EventAndCommands.Persitency` before the rename lands | Rename committed after the tests | No | Self-inflicted churn — ordering was never specified |
| 9 | Scale/load | The 10-minute budget has no measurement or alert mechanism | Test count grows over time | Partial | Budget silently blown; the revisit trigger never fires |
| 10 | Invalid input | OpenAPI generation runs while a service fails to boot | Broken service during the generation step | No | Empty or stale spec committed; baseline silently lost |
| 11 | Migration/transition | Nothing verifies the committed spec still matches the running app | Route changed without regenerating | No | **The baseline becomes a lie — the precise failure the spec decision was meant to prevent** |
| 12 | User flow branch | Integration job red during the "10 green runs before it's a gate" window | Any failure before §7 is amended | No | Red job ignored; harness rots before it ever becomes a gate |
| 13 | Permission boundary | Calendar gets route-contract only, but F-016 will add ownership guards to it | F-016 modifies Calendar's two routes | No | F-016's guard changes land unprotected by the harness |

### Triage Decisions

**User decision: ALL 13 IN SCOPE.**

| # | Decision | Acceptance criterion captured |
|---|---|---|
| 1 | In scope | The harness detects an unreachable Docker daemon and fails with an actionable message naming Rancher Desktop and the `~/.rd/bin` PATH requirement — not a raw Testcontainers stack trace. |
| 2 | In scope | Image pull failures are surfaced as an infrastructure error distinct from a test assertion failure; images are pinned by tag so a pull is reproducible. |
| 3 | In scope | The harness detects a running AppHost (or its containers) and warns before competing for a 2-CPU VM. |
| 4 | In scope | Containers are reaped automatically after an abnormal exit (Testcontainers' resource reaper enabled and verified by killing a run mid-flight). |
| 5 | In scope | **A permanent guard test asserts the EventStore write is reachable on the command path**, so a forgotten mutation restore cannot silently delete the invariant. The manual red/green is done once as evidence; the guard is what survives. |
| 6 | In scope | The token factory can mint an **expired** token, making the 401 path testable despite `ClockSkew = TimeSpan.Zero`. |
| 7 | In scope | The token factory can mint a token for an **arbitrary subject**, making the 403 ownership path testable. |
| 8 | In scope | Ordering is fixed: **the `Persistence` rename lands before the integration tests are written**, so no test is authored against the misspelled namespace. |
| 9 | In scope | The CI integration job reports its wall-clock duration, and exceeding **10 minutes** fails or warns explicitly — making the container-lifetime revisit trigger fire on data rather than on someone noticing. |
| 10 | In scope | OpenAPI generation **fails loudly** if any service cannot boot; it never writes an empty or partial spec over a good one. |
| 11 | In scope | CI regenerates the spec and **fails on any diff against the committed copy**, so the baseline cannot drift into being a lie. |
| 12 | In scope | The pre-gate window has defined semantics: a red integration job **blocks the PR from the first run**, with the 10-green-run count governing only when §7 is formally amended — not whether failures may be ignored. |
| 13 | In scope | **Calendar gets full tier coverage too.** All 7 services get all applicable tiers. *(Tier 3 does apply to Calendar: query handlers also persist audit events — `GetAllCustomersEvent` and siblings exist — so a read path is auditable and therefore guardable.)* |

### Scope note — recorded honestly

Triaging all 13 in scope **grew F-018** after the Adversarial Review follow-up had deliberately kept its endpoint coverage at smoke-test level. These are not in conflict, and the combination is coherent, but the shape should be stated plainly in the PRD:

> **F-018 delivers thin endpoint coverage on a thoroughly robust harness.** The number of endpoint tests stays at roughly one per tier per service (a smoke test, not a regression net — building the net is F-019's job), while the harness's own failure modes, diagnostics, cleanup, token capabilities, budget measurement and spec-drift detection are all held to acceptance criteria.

That is defensible: a fragile harness with broad coverage is worse than a dependable harness with thin coverage, because the first teaches people to distrust red builds. But it does mean **items 6, 7, 9, 11 and 12 add real work** — a token factory with time control, CI duration enforcement, and a spec-drift check are each non-trivial. Plan must size them rather than treat them as incidental.

## UX Discovery

**Skipped:** F-018 has no UI surface — it delivers an integration-test harness, a namespace rename, CI changes, an OpenAPI spec and constitution amendments. The user also declined the visual companion at Step 1, so the step's visual precondition was unmet regardless. Muse abstained from the Progressive Thinking meeting for the same reason. Design Step 10.6 (Design-Laws Audit) will correctly triage to **Skip**, and no `ux-review.md` will exist — which in turn means Ship Step 11.5 (UX Verify) and the METRICS UX-scorecard row are correctly omitted.

## Capability Scope Check

**Skipped:** no `control-manifest.toml` at the repo root, so this repo is not part of a pdlc-fy multi-repo capability and has no sibling repos to scope work against. `node scripts/capability.cjs read --json` was not run because the manifest's absence already settles it. Agenda Buddy is a standalone repo.

## Standards Guidance (ideation)

**Skipped — inputs unavailable. This is not an override.**

The `nordstrom-standards-readiness` plugin **is installed** (verified at preflight), but its six `.nordstrom-standards/*` source repositories do **not resolve** under this machine's git/gh auth — probed directly on 2026-08-18 (`engineering`, `security`, `privacy` all unreachable), and there is no local `.nordstrom-standards/` checkout. The same condition blocked Step 12.6 during F-013.

Step 6.5's enforcement tier is **`advisory`**, so this skips with notice rather than blocking. No `docs/standards-readiness/ideation-*.md` was produced.

Secondary note: Agenda Buddy is a personal project under `fererelabs`, not a Nordstrom system, so the six Nordstrom standards bodies are of questionable applicability here regardless of reachability. Worth settling deliberately rather than leaving the gate to fail silently at every feature — a `/diagnose` follow-up.

No `⚠ MUST` items were raised, because no analysis ran. The Plan gate (Step 18.5, `--design`) will re-attempt and is expected to skip for the same reason.

## Discovery Summary

**Confirmed by the user:** 2026-08-18T15:25:00Z

**Feature:** F-018 `api-refactor-foundations` — stage 1 of 3 in the API refactor program (F-018 → F-019 → F-020)

**Problem:** The endpoint layer has ten evidenced defects — success decided by `StartsWith("exception")`, discarded cancellation tokens, MongoDB entities serving as the public API contract, and MediatR registered but never dispatching. Fixing them means rewriting all 7 services' endpoint layers, and **there are currently zero integration tests** to catch a regression while doing so. F-018 builds that net first, because episode 001 concluded both of F-013's real defects were invisible to review and surfaced only by running the software.

**User:** This repo's developers (one maintainer + AI agents). No provider- or customer-facing change. The real consumers are F-019/F-020, which cannot safely proceed without it.

**Success metric:** F-018 delivers a **working harness, not a regression net** — deliberately accepted. Concretely: the harness runs and can assert all three tiers (route contract · persistence round-trip against real MongoDB · the EventStore audit actually firing) for every service. The Q3 zero-count metrics belong to F-019/F-020, which touch endpoints.

**Technical constraints:**
- Three hard prerequisites; nothing runs until all three work: `InternalsVisibleTo` on 7 service `.csproj` files (`Program` is internal — top-level statements); an RSA keypair before host build (`AddAgendaBuddyAuthentication()` **throws** without `JWT_PUBLIC_KEY`); Mongo connection string injected via configuration.
- Tokens must be RS256, issuer `agenda-buddy-identity`, unexpired, `ClockSkew = TimeSpan.Zero`.
- OTLP export is already conditional and **needs no suppression** (corrected from a wrong inference).
- Local dev is Rancher Desktop: 2 CPUs / 4.1 GB already running k8s, `docker` not on PATH.
- CONSTITUTION §3 invariants must survive: MediatR CQRS, EventStore audit, cache-aside.
- Container-per-test (user's choice), with **10 minutes** as the objective revisit trigger.

**Out of scope:**
- All endpoint-layer changes → **F-019** (pilot: Booking) and **F-020** (remaining 6).
- Extracting shared abstractions into a common project → F-020's decision, after F-019 shows what generalises.
- Container image pinning/caching beyond reproducibility → only if the 10-minute budget is approached.
- Container/CD hardening and the §7 security-scan gate → remains **F-017**.

**Key risks / assumptions:**
- ⚠️ **Testcontainers on Rancher is unverified** and everything depends on it. Needs a spike **first**.
- ⚠️ **No OpenAPI generation mechanism exists.** Swashbuckle generates from a running app; Swagger runs only in `Development`; the build-time alternative is an unmentioned sixth dependency.
- **No rollback story** — F-018 edits 7 production `.csproj` files and renames a namespace.
- **Cache-aside gets no guard** while the audit invariant gets two — asymmetric, unexplained.
- **7 MobileApp tests are skipped and nobody knows why** (372 of 379 actually execute).
- **Triaging all 13 edge cases in scope grew the feature**: a token factory with time control, CI duration enforcement, and spec-drift detection are each non-trivial and must be sized at Plan.
- **Sequencing decided:** F-016 ships **before** F-019/F-020 — recorded as `F-019 depends_on [F-018, F-016]`.
