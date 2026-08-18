---
feature: refactor-minimal-apis
date: 2026-08-18
status: in-progress
last-updated: 2026-08-18T13:50:00Z
approved-by:
approved-date:
prd:
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
_Not run._

## Adversarial Review
_Not run._

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
_Not run._

## Discovery Summary
_Pending._
