# Constitution
<!-- pdlc-template-version: 2.5.0 -->
<!-- This file is the single source of truth for how this project is built.
     PDLC reads it before every phase. Strong defaults are already set.
     Override only what your team explicitly agrees to change.
     Edits to this file are logged by the guardrails hook for `/diagnose`
     reconciliation. -->

**Version:** 1.0.0
**Last updated:** 2026-09-12
**Project:** Agenda Buddy

---

## 1. Tech Stack Decisions

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| Language | C# 14 / .NET 10 (`net10.0`, implicit `LangVersion`) | Primary language; nullable-enabled, implicit usings. Upgraded from .NET 8 by F-011 (`v0.1.0`-era) |
| Runtime / Framework | ASP.NET Core 10 Minimal APIs | Lightweight, fast, per-microservice entry point |
| Messaging / Events | MediatR 12 + synchronous notification dispatch | CQRS dispatch is in-process; there is no message broker or outbox |
| Database | MongoDB (via MongoDB.Driver 2.25) | Document model suits flexible provider/customer/appointment nesting |
| Caching | IDistributedCache (Microsoft.Extensions.Caching) | Cache-aside pattern for read performance |
| Testing | xUnit | Unit and integration test framework across all *.Tests projects |
| Containerization | .NET Aspire + .NET SDK container publishing | Aspire is the primary local orchestrator; Docker Compose is a legacy fallback |
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
| Properties | PascalCase | `EmailProvider`, `AvatarId` |
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

- **Service isolation**: Booking, Calendar, Customer, Provider, Services, Profession, and Identity are independent ASP.NET Core services; the Gateway is a separate YARP process
- **Clean Architecture split**: the six CQRS services each use `*.Api`, `*.Core`, `*.Domain`, and deliberately empty `*.Infrastructure` projects; Identity remains a direct-service exception
- **Shared Library pattern**: shared entities, repositories, domain services, and cross-cutting tools live in `AgendaBuddy.Library`
- **CQRS via MediatR**: commands, queries, and handlers live in each service's own Core/Domain projects; `AgendaBuddy.EventAndCommands` contains audit infrastructure only
- **Event sourcing (audit trail)**: every command result (success or fail) is persisted to the `EventStore` (MongoDB) — do not remove this pattern
- **Cache-aside pattern**: the `CacheAside` extension on `IDistributedCache` (semaphore-guarded double-checked locking) must be used for all read-heavy queries — do not bypass it with direct cache calls
- **No message broker**: customer messaging is MongoDB-backed and notification fan-out is synchronous, in-process, and best-effort through `INotificationDispatcher`

---

## 4. Security & Compliance Requirements

- Transport security is centralized in `UseAgendaBuddyTransportSecurity()`, immediately before `UseAuthentication()` in all seven services
- Anti-CSRF protection (`AddAntiforgery` / `UseAntiforgery`) is enabled in the six CQRS APIs; Identity uses its dedicated authentication route protections
- Input validation uses Validot where migrated and data annotations at remaining API boundaries; do not add new MiniValidator usage
- Secrets must never appear in source code — use `appsettings.json` / User Secrets / environment variables
- JWT authentication and ownership/role authorization are mandatory on every non-public route
- PII is stored in MongoDB; preserve ownership guards, bounded audit retention, and OpenTelemetry PII redaction

---

## 5. Definition of Done

- [ ] Code is committed on the feature branch with a conventional commit message
- [ ] Backend tests pass (`dotnet test agenda-buddy-backend.slnf`)
- [ ] Integration tests pass (`dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj /p:MobileWorkloads=false`)
- [ ] Mobile tests pass (`dotnet test AgendaBuddy.MobileApp.Tests/AgendaBuddy.MobileApp.Tests.csproj /p:MobileWorkloads=false`)
- [ ] Code has been reviewed by Neo, Echo, Phantom, and Jarvis
- [ ] Review file (`docs/pdlc/reviews/REVIEW_*.md`) exists and is human-approved
- [ ] No debug/placeholder code left in committed files
- [ ] All public service methods have XML doc comments
- [ ] Build passes (`dotnet build --no-restore`)
- [ ] No compiler warnings promoted to errors
- [ ] PR description is complete and references the Beads task ID
- [ ] Episode file drafted and human-approved
- [ ] New service (if any) is wired into AppHost, Gateway routing, CI path filters, and SDK container publishing

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
- `fix(notifications): preserve unread count when refresh fails`
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

### Ship artifacts are tag plus GitHub Release — both are mandatory

A PDLC ship is incomplete until the version exists as both an annotated git tag and a published GitHub Release.
Pushing a tag alone does not create a GitHub Release and must never be reported as if it did.

After the ship PR is green, merged, and any required post-merge deployment is verified:

1. Update `CHANGELOG.md` for the version before tagging.
2. Create an annotated `vX.Y.Z` tag on the verified merge commit and push that tag to `origin`.
3. Create the GitHub Release through the GitHub REST API using the `ogdevlabs` git-credential token. Do not use
  the restricted `gh` identity. The release must target the existing tag, be non-draft/non-prerelease unless the
  release plan says otherwise, include verification evidence and a compare link, and mark the newest stable
  version as latest.
4. Verify independently before declaring Ship complete:
  - `git ls-remote --tags origin refs/tags/vX.Y.Z` returns the tag;
  - `GET /repos/ogdevlabs/agenda-buddy/releases/tags/vX.Y.Z` returns a published release;
  - the tag resolves to the intended merge commit;
  - the repository's latest release is the new stable version.
5. If release publication or any verification fails, repair it in the same Ship operation. Do not close the
  feature, mark the episode Final, or report the release shipped while either artifact is missing.

**Also standing:** the Nordstrom Standards Readiness gate does not apply to this project at all (see §9, ADR-042) — do not detect, install, prompt for, or invoke it here, regardless of what a generic skill's preflight checks for.

---

## 7. Test Gates

- [x] Unit tests
- [x] Integration tests
- [ ] E2E tests (real Chromium)
- [ ] Performance / load tests
- [ ] Accessibility checks
- [ ] Visual regression tests
- [x] Security scan (dependency audit + secret scan — always required, cannot be unchecked)

| Name | Command | Required |
|------|---------|----------|
| Backend tests | `dotnet test agenda-buddy-backend.slnf --collect:"XPlat Code Coverage"` | yes |
| Integration tests | `dotnet test AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj /p:MobileWorkloads=false` | yes |
| Mobile tests | `dotnet test AgendaBuddy.MobileApp.Tests/AgendaBuddy.MobileApp.Tests.csproj /p:MobileWorkloads=false` | yes |

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
  `AgendaBuddyExceptionHandler`. `DataResponse<T>` is authored in-repo instead. `FluentResults` and `Validot`
  are now used in production; Mapster remains approved but has no call sites.
- All database migrations (schema changes) must be documented in DECISIONS.md before implementation
- ~~The `EventAndCommands/Persitency/` typo is a known issue — do not rename until a dedicated refactor is planned (renaming breaks existing references)~~ **RETIRED 2026-08-18 by F-016-T01.** The clause's own stated condition — *"until a dedicated refactor is planned"* — was satisfied by the approved F-016 PRD, so the prohibition expired on its own terms. Its stated *reason* also turned out to be wrong: the rename did **not** break references across all consumers. Measured before the change and confirmed after: **11 `.cs` files, one reference each, and zero references in any `.json`, `.yml`, `.csproj` or `.slnf`.** The directory and namespace are now `EventAndCommands/Persistence/`, pinned by `EventsAndCommands.Tests/Persistence/PersistenceNamespaceTest.cs` so a revert fails a test rather than passing silently.
- Kafka was removed in 2026-09 because it had no producers or consumers; do not reintroduce a broker without a new architecture decision
- **The Nordstrom Standards Readiness gate does not apply to this project (ADR-042, 2026-08-23).** Agenda
  Buddy is a personal `fererelabs` project, not a Nordstrom enterprise engagement — the six standards bodies
  the plugin assesses against were never applicable, independent of the ten consecutive gates that also
  failed to reach the plugin's source repos under this machine's `gh` auth. **No future `/brainstorm`,
  `/build`, `/ship`, or `/hotfix` gate call site should prompt for or attempt this check on this
  repository.** This retires the standing F-017 backlog item ("give the standards gate a reachable source or
  retire it explicitly") — retirement was the answer.
