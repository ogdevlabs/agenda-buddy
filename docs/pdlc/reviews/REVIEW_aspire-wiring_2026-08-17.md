# Review — aspire-wiring (F-013)

**Date:** 2026-08-17 · **Branch:** `feat/F-013-aspire-wiring` · **Diff:** 130 files, +5323/−320, 16 commits
**Reviewers:** Neo (architecture + YAGNI), Phantom (security), Jarvis (docs/contracts)
**Blast radius:** `docs/pdlc/reviews/BLAST-RADIUS_aspire-wiring_2026-08-17.md`
**Attestation:** `docs/pdlc/design/aspire-wiring/verification.md`

## Tally

| Severity | Count |
|---|---|
| 🔴 Critical | **0** |
| 🟡 Important | 3 (2 already fixed during review) |
| ⚪ Advisory | 9 |
| Over-engineering (deletion opportunities) | 3 |

**No Critical findings. No merge blockers from this review.**

> ⚠️ **Reviewer gap — recorded, not glossed.** **Echo (test coverage) did not report.** The agent was spawned with full context, went idle without producing findings, and did not answer a follow-up request. Per the orchestrator's spawn-failure rule the round continued with 3 of 4 reviewers. **Consequence: this review carries no independent test-coverage verdict.** Coverage evidence therefore rests on Neo's own attestation in `verification.md` (AC→test mapping) and the blast-radius untested-path list — i.e. self-assessed, not independently audited. Anyone relying on this review should treat test adequacy as unreviewed. Re-running Echo alone would close it.

---

## 🟡 Important

**I-1 — CI credential guard exempted the directory that already leaked.** *(Phantom)* · **FIXED during review**
`.github/workflows/dotnet.yml` scanned for credentials with `':(exclude)docs/pdlc'`. Two of the seventeen files that carried the live Atlas credential were under `docs/pdlc/context/` (copied there by the hydrate-context backfill), so the guard blanket-exempted the one tree with a proven record of ingesting secrets. Today's files are correctly redacted, but nothing would have caught a real credential pasted into a future PRD or brainstorm doc.
**Fix applied:** the guard now scans `docs/pdlc` as well, filtering known redaction placeholders instead of excluding the tree. Verified passing.

**I-2 — New HTTP surface undocumented where consumers look.** *(Jarvis)* · **FIXED during review**
`/health` and `/alive` were documented in `ARCHITECTURE.md` §5 and the threat model, but absent from the README — the file an ops engineer or integrator actually opens. Wiring a load balancer or k8s probe required digging through `docs/pdlc/design/`.
**Fix applied:** README gained a *Health endpoints* section giving path, purpose, status codes, the readiness/liveness split and why it matters, the bare-status response body, and the 5s cache.

**I-3 — `IMongoDbConfiguration` is a dead abstraction whose wiring this feature made more elaborate.** *(Neo)* · **open — recommend fix**
`grep` finds it registered in all 7 `Program.cs` and injected **nowhere**. It was already effectively dead (the old code called `new MongoDbConfiguration(configuration).MongoClient()` directly inside the extension, never through DI). This feature replaced that with an explicit 2-line factory registration in each of 7 files — 14 lines of new wiring for an abstraction nothing resolves.
**Recommendation:** delete the registration from all 7 `Program.cs`. The 3 existing tests pin the concrete class, not the interface, so AC-5.2 is unaffected. This also removes the `(MongoClient)` cast from every production path, retiring the tech-debt item below. Deferring is defensible — it is dead either way — but the diff is smaller and safer *now* than later.

---

## ⚪ Advisory

**A-1 — Conformance was partly achieved by editing the specification.** *(Neo)* ARCHITECTURE.md §3.2, §3.3, §3.5 and §9 were changed to match the implementation. Each change was because the design was wrong (the ctor claim, the port assumption) or silent (`ResolveSetting`'s signature, the cache design, the `Library` package additions), and each is annotated with why. A reader should still know the design moved toward the code, not only the reverse.

**A-2 — `MongoHealthCheck` rolls its own cache instead of reusing `CacheAside`,** which CONSTITUTION §3 mandates for read-heavy reads. *(Neo)* Justified: `CacheAside` JSON-serialises values (a `HealthCheckResult` carries an `Exception`), would drag `IDistributedCache` into `Library/Diagnostics/`, and has a `static` semaphore shared across all keys plus a `default!` return on lock timeout that would silently report Unhealthy. The rationale belongs in the code comment, not just this review.

**A-3 — T-003's test asserts the flag, not the mechanism.** *(Phantom)* `JwtKeyIsASecretParameter` asserts `ParameterResource.Secret == true`; it does not prove masking survives `WithEnvironment(...)` into the dashboard. Aspire keys masking to the parameter reference so it should hold, but it is asserted by inspection. → fold into F-013-T14.

**A-4 — T-001 rotation was missing from Active Blockers.** *(Phantom)* · **FIXED during review** — documented in four places but absent from the list a handoff reader scans first. Now the top entry in `STATE.md` Active Blockers.

**A-5 — `docs/pdlc/context/06-configuration.md` is stale in a way that misleads.** *(Jarvis)* Its "single most serious finding" section still names the committed Atlas credential with line numbers; a reader following those lines today finds empty keys. Catalog refresh happens at Ship (Reflect 16c-bis), but this section could send someone chasing an already-closed finding. A one-line "superseded by F-013, see ADR-013" pointer would cover the gap meanwhile.

**A-6 — README's `STRIPE_SECRET_KEY` row is false, and this feature carried it forward.** *(Jarvis)* Nothing reads that variable; `StripePaymentGateway` takes `apiKey` as a raw ctor string with no DI registration. Pre-existing (confirmed against `main`), but F-013 rewrote this exact table and reproduced the inaccuracy. Out of scope to fix here.

**A-7 — XML doc coverage exceeds CONSTITUTION §5.** *(Jarvis)* Verified across every new public type plus the 7 rewritten `MongoDbConfiguration` classes, including the load-bearing "why" comments. No gap — recorded so the absence of a finding is not mistaken for absence of a check.

**A-8 — README's resolution order matches the code exactly.** *(Jarvis)* `README.md:197` against `MongoConnectionResolver.ConnectionStringKeys` — verified, no drift.

**A-9 — All 16 commit messages are Conventional-Commits compliant,** and the `!` on `d00b87f` is justified: standalone and Compose runs previously connected silently to a shared Atlas cluster and now fail fast. *(Jarvis)*

---

## Over-Engineering (deletion opportunities — not blockers)

- `delete:` the 7 `AddSingleton<IMongoDbConfiguration>` factory registrations — dead abstraction (I-3).
- `shrink:` the 7 near-identical `ServiceCollectionMongoResolutionTest.cs` files (~150 lines each). A shared theory over a `(configuration → services)` delegate would halve them without losing per-service R-3 coverage. Echo flagged near-duplicate test debt at the Wave 2 standup; this repeats the pattern deliberately, but it will rot.
- `yagni:` the 7 `MongoDbConfiguration` classes and 7 `IMongoDbConfiguration` interfaces are now kept alive solely by 3 tests. Follow-up deletion candidate; not this feature's job.

## Tech debt introduced

| Debt | Repayment condition |
|---|---|
| `(MongoClient)` cast in 7 classes — throws `InvalidCastException` if an `IMongoClient` test double is registered | Disappears when I-3 is applied |
| `AppHostWiring` mutates `EndpointAnnotation.Port`/`TargetPort` on annotations Aspire produced | Revisit on any Aspire major upgrade; test-guarded meanwhile |

## Security sign-off (Phantom)

**Posture: WARNINGS — proceed.** Zero Critical. Every "mitigate now" threat has real, tested code — no citation-only mitigations. `tasks.cjs check --json` → `{"findings":[]}`, no `security-ac-untested`. Independently verified sound: T-002's 5s cache (asserted by call count, not sleep); the 14 emptied `ConnectionString` keys failing safe through the resolver rather than reaching `MongoClient("")`; credential removal confirmed by his own `git grep`; `/health`/`/alive` leaking no check names or exception detail via ASP.NET's default writer; and the 3 CVE pins in `Directory.Build.props` as an existing mitigation for the 2.25.0 driver pin. Anonymous probes judged reasonable and dwarfed by six pre-existing anonymous PII endpoints already deferred to F-016.

## Cross-talk links

- **I-1 ↔ T-009's own discovery.** The credential removal found 17 files rather than 14 precisely because `docs/pdlc/context/` had ingested the URI; the guard then exempted that tree. Same root cause — docs are a secret-ingestion path — one fix.
- **A-3 ↔ verification.md T-004.** Both are dashboard-observable claims that cannot be checked without a container runtime; both belong to F-013-T14 rather than being separately deferred.
- **I-2 ↔ Phantom's anonymous-probe assessment.** Same README surface: documenting the endpoints and noting they are anonymous is one edit.

## CHANGELOG (draft, Jarvis)

```markdown
## [Unreleased]

### Added
- **Aspire local orchestration.** `dotnet run --project AgendaBuddy.AppHost` starts MongoDB, Kafka, and all seven API services with one command, replacing manual per-service runs plus an undocumented `.env`.
- `/health` and `/alive` on all seven services — readiness vs liveness, so `/alive` stays healthy when MongoDB is unreachable and an orchestrator will not kill a process merely waiting on its database.
- OpenTelemetry traces, metrics, and structured logs via `AgendaBuddy.ServiceDefaults`, exported over OTLP under the AppHost.
- JWT keys supplied as Aspire secret parameters — prompted once, stored in user secrets, masked in the dashboard — instead of a gitignored `.env`.

### Changed
- All seven services share one process-wide `IMongoClient`, resolved by `MongoConnectionResolver` across four key shapes. `EventStore` alone previously opened a new client per HTTP request.
- Host ports are assigned dynamically instead of hardcoded `localhost:603x`; read the live port from the dashboard.
- Kafka's broker address is configuration-driven (`ConnectionStrings:kafka` / `Kafka:BootstrapServers`, default `localhost:9092`).
- CI builds `AgendaBuddy.AppHost` and targets `agenda-buddy-backend.slnf`; path filters extended to AppHost, ServiceDefaults, Dockerfiles, and workflow changes.

### Fixed
- Startup crash affecting six of seven services in `Development`: a captive dependency — singleton `IRequestCollection` consuming scoped `IEventStore`.
- `Profession` catalogue seeding no longer blocks startup on a synchronous `.Wait()`, and a seeding failure no longer prevents the service from starting.

### Security
- **Removed a committed MongoDB Atlas credential from all tracked files** (17 files). This does **not** remediate the disclosure — it remains in git history and must be rotated at Atlas. Standalone and Compose runs without `ConnectionStrings__mongodb` now fail fast with an actionable error instead of silently connecting to the shared cluster.
- Guarded the last possibly-null `MongoClient` construction path (the seven legacy `MongoDbConfiguration` constructors).

### Deprecated
- Docker Compose is retained but no longer the recommended local path — no health model, no telemetry, no connection-string injection.
```
