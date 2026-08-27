# Constitution
<!-- pdlc-template-version: 2.5.0 -->
<!-- This file is the single source of truth for how this project is built.
     PDLC reads it before every phase. Strong defaults are already set.
     Override only what your team explicitly agrees to change.
     Edits to this file are logged by the guardrails hook for `/diagnose`
     reconciliation. -->

**Version:** 1.0.0
**Last updated:** 2026-07-30
**Project:** Agenda Buddy

---

## 1. Tech Stack Decisions

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| Language | C# 14 / .NET 10 (`net10.0`, implicit `LangVersion`) | Primary language; nullable-enabled, implicit usings. Upgraded from .NET 8 by F-011 (`v0.1.0`-era) |
| Runtime / Framework | ASP.NET Core 10 Minimal APIs | Lightweight, fast, per-microservice entry point |
| Messaging / Events | Kafka (Confluent) + MediatR 12 | Async inter-service communication; CQRS command/event dispatch |
| Database | MongoDB (via MongoDB.Driver 2.25) | Document model suits flexible provider/customer/appointment nesting |
| Caching | IDistributedCache (Microsoft.Extensions.Caching) | Cache-aside pattern for read performance |
| Testing | xUnit | Unit and integration test framework across all *.Tests projects |
| Containerization | Docker + Docker Compose | Local development and deployment orchestration |
| CI/CD | GitHub Actions (.github/workflows/dotnet.yml) | Restore → Build → Test → Coverage on push/PR to main |

---

## 2. Coding Standards & Style

### Linting & Formatting

- Linter: `.editorconfig` at the repo root (F-018-T03) — encodes the conventions this table already documents plus indentation/brace-style/namespace rules, `dotnet_naming_rule.*` at `suggestion` severity
- Formatter: `dotnet format agenda-buddy-backend.slnf` — enforced in CI via `dotnet format --verify-no-changes` in the `build-and-test` job
- Pre-commit hook: none

### Naming Conventions

| Construct | Convention | Example |
|-----------|-----------|---------|
| Classes / Interfaces | PascalCase | `ProviderEntity`, `IBookingService` |
| Methods | PascalCase + Async suffix for async | `BookAppointmentAsync` |
| Properties | PascalCase | `EmailProvider`, `KafkaTopic` |
| Private fields | camelCase | `_collection` |
| Files | PascalCase matching class name | `BookingService.cs` |
| MongoDB BSON fields | snake_case via `[BsonElement]` | `email_provider`, `first_name` |
| Branch names | feature/[kebab-case] | `feature/user-auth` |
| Namespaces | PascalCase, mirrors directory | `Library.Services`, `EventAndCommands.Commands.Booking` |

### General Rules

- All business logic lives in the Library service layer — not in API handlers
- Repository pattern only — no direct MongoDB queries outside `MongoDbRepository<T>`
- Use `required` and `[EmailAddress]` data annotations on entity fields at the boundary
- Async all the way down — every I/O method returns `Task` or `Task<T>`
- No magic strings for MongoDB field names — use `[BsonElement]` attributes

---

## 3. Architectural Constraints

- **Service isolation**: each domain (Booking, Calendar, Customer, Provider, Services, Profession) is an independent ASP.NET Minimal API microservice with its own MongoDB config and Dockerfile
- **Shared Library pattern**: all domain entities, the generic `IRepository<T>` / `MongoDbRepository<T>`, and domain services live in the `Library` project — consumed by all microservices and EventAndCommands
- **CQRS via MediatR**: commands and queries are separated in `EventAndCommands`; handlers consume Library domain services; command handlers persist success/failure events to EventStore
- **Event sourcing (audit trail)**: every command result (success or fail) is persisted to the `EventStore` (MongoDB) — do not remove this pattern
- **Cache-aside pattern**: the `CacheAside` extension on `IDistributedCache` (semaphore-guarded double-checked locking) must be used for all read-heavy queries — do not bypass it with direct cache calls
- **Kafka per-provider topics**: each provider gets a dedicated Kafka topic (derived from email prefix) — maintain this convention for new provider-related commands

---

## 4. Security & Compliance Requirements

- HTTPS enforced (`UseHttpsRedirection`) in all microservices
- Anti-CSRF protection enabled (`AddAntiforgery` / `UseAntiforgery`) in all services
- Input validation: **today** `MiniValidator.TryValidate` (or data annotations) runs at the top of every
  API endpoint. **Target, per ADR-016 (2026-08-18):** `Validot` replaces `MiniValidator`, with validation
  moved off the endpoint into a shared validation base class — this removes the ~duplicated
  `MiniValidator.TryValidate` block currently repeated across all seven `Program.cs` files. **The transition
  has not happened yet** — F-018 (this feature) approves the package and records the target; the endpoint
  rewrite lands in F-019 (pilot, `Booking` only) and F-020 (rollout, the remaining six services). Until then,
  this section describes both the current code (`MiniValidator`) and the destination (`Validot`) rather than
  only one or the other.
- Secrets must never appear in source code — use `appsettings.json` / User Secrets / environment variables
- No authentication or authorization middleware exists yet *(inferred — please implement before public exposure)*
- PII (email addresses) is stored in MongoDB — ensure access controls are in place

---

## 5. Definition of Done

- [ ] Code is committed on the feature branch with a conventional commit message
- [ ] All unit tests pass (`dotnet test`)
- [ ] All integration tests pass
- [ ] Code has been reviewed by Neo, Echo, Phantom, and Jarvis
- [ ] Review file (`docs/pdlc/reviews/REVIEW_*.md`) exists and is human-approved
- [ ] No debug/placeholder code left in committed files
- [ ] All public service methods have XML doc comments
- [ ] Build passes (`dotnet build --no-restore`)
- [ ] No compiler warnings promoted to errors
- [ ] PR description is complete and references the Beads task ID
- [ ] Episode file drafted and human-approved
- [ ] New microservice (if any) has a Dockerfile and is wired into docker-compose

---

## 6. Git Workflow Rules

### Branch Strategy

- **Feature branch model**: one branch per feature (`feature/[feature-name]`), single PR to `main` at end of Construction.

**Default branch:** `main`
**Feature branch naming:** `feature/[kebab-case-feature-name]`
**Merge strategy:** Merge commit (preserves full branch history)

### Commit Message Format

Format: `<type>(<scope>): <description>`

Types: `feat` | `fix` | `chore` | `docs` | `test` | `refactor` | `perf` | `ci`

Examples:
- `feat(booking): add appointment cancellation endpoint`
- `fix(kafka): make bootstrap servers configurable via appsettings`
- `test(provider): add unit tests for DeactivateProviderCommandHandler`

**Breaking changes:** append `!` after type, e.g. `feat(api)!: rename /appointments endpoint`

### Protected Branches

- `main` — requires PR + human approval. **No exceptions for tooling failures** — see the `gh` restriction below.

### CI must be green before merge — no exceptions

**Stated explicitly 2026-08-27.** Once a PR is open, its CI run (`.github/workflows/dotnet.yml`'s jobs — `build-and-test`, `security-scan`, `docker-build-and-scan` matrix, `integration`, mobile builds, whichever the change triggers) must be **polled to completion and confirmed green** before that PR is merged. Never merge speculatively, never merge while a check is still queued/running, and never merge on the assumption that a passing local run implies a passing CI run — they can diverge (container runtime differences, Docker matrix jobs, mobile workloads, drift checks). If a check fails, fix it and push a new commit to re-trigger CI; do not merge around a red check. This applies to every PR this project opens, including PDLC ship-bookkeeping PRs, not just feature-code PRs.

### `gh` CLI is restricted on this repo — PR-based merge is mandatory, not optional

**This has been violated repeatedly (F-017 through F-020) and is not acceptable going forward.** The `gh` CLI on this machine authenticates as `OscarPaul-GarciaCapetillo_NordTech`, a Nordstrom work identity that is **READ-only** on `ogdevlabs/agenda-buddy` — `gh pr create`, `gh pr merge`, and `gh pr edit` all fail with `GraphQL: Unauthorized: As an Enterprise Managed User, you cannot access this content`. That failure is **not** evidence that PR-based workflow is impossible here, and it must never be treated as license to bypass `main`'s "requires PR" rule with a local `git merge --no-ff` + `git push origin main`. Every one of F-017–F-020's ship episodes did exactly that, skipping the PR entirely — a process failure, confirmed avoidable on 2026-08-27 when PR #70 was opened *and* merged cleanly with no `gh` involved at all.

**The required path, in order:**
1. **Commit and push to a feature/chore branch**, never directly to `main`.
2. **Open the PR via the GitHub REST API**, authenticated with the `ogdevlabs` git-credential token (the same one `git push` already uses) — never `gh`:
   ```bash
   TOKEN=$(printf "protocol=https\nhost=github.com\npath=ogdevlabs/agenda-buddy.git\n\n" \
     | git credential fill | sed -n 's/^password=//p')
   curl -s -X POST -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.github+json" \
     https://api.github.com/repos/ogdevlabs/agenda-buddy/pulls -d @pr.json
   ```
3. **Merge via the GitHub web UI, or the same REST token against `PUT .../pulls/<number>/merge`** — not `gh pr merge`, and not a local `git merge --no-ff` bypass. Only fall back to a local merge + push if both of those are demonstrably attempted and fail (not just assumed to fail because `gh` did), and only with the human's explicit confirmation.
4. Use the `.gitconfig`-configured identity (`ogdevlabs`) for every commit — never pass `-c user.name`/`user.email` overrides, and never route commits through `gh`'s identity.

**Also standing:** the Nordstrom Standards Readiness gate does not apply to this project at all (see §9, ADR-042) — do not detect, install, prompt for, or invoke it here, regardless of what a generic skill's preflight checks for.

---

## 7. Test Gates

- [x] Unit tests
- [ ] Integration tests
- [ ] E2E tests (real Chromium)
- [ ] Performance / load tests
- [ ] Accessibility checks
- [ ] Visual regression tests
- [x] Security scan (dependency audit + secret scan — always required, cannot be unchecked)

| Name | Command | Required |
|------|---------|----------|
| .NET unit tests | `dotnet test --collect:"XPlat Code Coverage"` | yes |

---

## 8. Context & Model Configuration

**Context window (tokens):** 1000000
**Warning threshold:** 60
**Critical threshold:** 75
**Distill threshold (tokens):** 800
**Interaction Mode:** Sketch

---

## 9. Additional Rules

- New packages require discussion before adding — keep the dependency footprint minimal. **Four packages
  pre-approved per ADR-015 (2026-08-18) as amended by ADR-049 (2026-08-26):** `FluentResults`, `Validot`,
  `Mapster`, `GuardClauses`. **`SmallApiToolkit` was approved by ADR-015 and then dropped by ADR-049** — a
  pre-Design spike found its `DataResponse<T>`/validation-base-class "narrow slice" doesn't exist in the
  package (those were the reference repo's own types); its dispatch abstraction was already rejected by
  ADR-014 (MediatR is the sole dispatcher) and its `ExceptionMiddleware` would duplicate F-016's
  `AgendaBuddyExceptionHandler`. `DataResponse<T>` is authored in-repo instead. Approved for **F-019/F-020**,
  not F-018 — no production code consumes them yet as of this feature.
- All database migrations (schema changes) must be documented in DECISIONS.md before implementation
- ~~The `EventAndCommands/Persitency/` typo is a known issue — do not rename until a dedicated refactor is planned (renaming breaks existing references)~~ **RETIRED 2026-08-18 by F-016-T01.** The clause's own stated condition — *"until a dedicated refactor is planned"* — was satisfied by the approved F-016 PRD, so the prohibition expired on its own terms. Its stated *reason* also turned out to be wrong: the rename did **not** break references across all consumers. Measured before the change and confirmed after: **11 `.cs` files, one reference each, and zero references in any `.json`, `.yml`, `.csproj` or `.slnf`.** The directory and namespace are now `EventAndCommands/Persistence/`, pinned by `EventsAndCommands.Tests/Persistence/PersistenceNamespaceTest.cs` so a revert fails a test rather than passing silently.
- Kafka `BootstrapServers` must be moved to configuration before any non-local deployment
- **The Nordstrom Standards Readiness gate does not apply to this project (ADR-042, 2026-08-23).** Agenda
  Buddy is a personal `fererelabs` project, not a Nordstrom enterprise engagement — the six standards bodies
  the plugin assesses against were never applicable, independent of the ten consecutive gates that also
  failed to reach the plugin's source repos under this machine's `gh` auth. **No future `/brainstorm`,
  `/build`, `/ship`, or `/hotfix` gate call site should prompt for or attempt this check on this
  repository.** This retires the standing F-017 backlog item ("give the standards gate a reachable source or
  retire it explicitly") — retirement was the answer.
