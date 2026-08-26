# Review: api-refactor-foundations (F-018)

**Task ID:** F-018 (all 21 tasks) · **Date reviewed:** 2026-08-26 · **Feature branch:** `feat/F-018-api-refactor-foundations`
**PRD:** [PRD_F-018_api-refactor-foundations_2026-08-18.md](../prds/PRD_F-018_api-refactor-foundations_2026-08-18.md)
**Blast radius:** [BLAST-RADIUS_api-refactor-foundations_2026-08-26.md](BLAST-RADIUS_api-refactor-foundations_2026-08-26.md)
**Verification:** [verification.md](../design/api-refactor-foundations/verification.md)

**Spawn mode note:** run in **Solo mode** (party-review orchestration forbade sub-agent spawning in this
execution context). Neo/Echo/Phantom/Jarvis findings below were produced by direct source verification —
every finding below was checked against the actual file, not inferred from the diff hunk alone; file paths
are cited so a human can re-walk the reasoning.

---

## Reviewers

| Reviewer | Role | Present |
|----------|------|---------|
| Neo | Architect | yes (solo) |
| Echo | QA Engineer | yes (solo) |
| Phantom | Security Reviewer | yes (solo) |
| Jarvis | Tech Writer | yes (solo) |
| Muse | UX Designer | **not joined** — `ux-review.md` triage was Skip (no UI surface) |

---

## Neo's Findings — Architecture & PRD Conformance

**PRD conformance:** Fully conformant, with 2 disclosed gaps (AC-11, AC-14 never built) and 3 disclosed deviations (AC-17/19's commit-baseline change, AC-22's headline count) — all already named in `verification.md`, none newly discovered here.
**Design doc adherence:** Followed, with one stale doc found this review (see finding N2).

### Findings

**[Important] N1 — `EventStoreWriteGuardTest`'s permanent guard proves less than AC-15 claims, and the gap is not hypothetical**

`AgendaBuddy.IntegrationTests/Audit/EventStoreWriteGuardTest.cs:94-100`:
```csharp
[Theory]
[MemberData(nameof(HandlerFileNames))]
public void AC15_EveryCommandOrQueryHandler_CallsEventStoreSaveAsync(string handlerFilePath)
{
    var content = File.ReadAllText(handlerFilePath);
    Assert.Contains("eventStore.SaveAsync(", content, StringComparison.Ordinal);
}
```
This asserts the literal substring `eventStore.SaveAsync(` appears **anywhere in the whole file** — not that
every code path (success and failure) calls it. AC-15 is worded "fails when the EventStore write is removed
from the command path" (singular path implied per branch), but the mechanism as built only fails when the
substring is removed from the *entire file*.

**This is not a theoretical gap — it already missed the exact defect this session found by hand.**
`EventAndCommands/Commands/Services/UpdateServicesFromProviderCommandHandler.cs:20,42,54`:
```csharp
if (existingProvider == null) return null!;      // line 20 — no audit, returns immediately
...
    await eventStore.SaveAsync(successEvent);      // line 42
...
    await eventStore.SaveAsync(failEvent);         // line 54
    return null!;
```
The not-found branch (line 20) writes no audit event at all — a real CONSTITUTION §3 gap, filed as
`agenda-buddy-f49` during Build. But the file still contains `eventStore.SaveAsync(` twice (lines 42, 54), so
`EventStoreWriteGuardTest` passes on this exact file. The guard would not have caught this gap; it was found
by T13's own manual per-branch reading, not by the "permanent" mechanism meant to catch regressions like it.

**Recommendation:** either (a) narrow AC-15's claim in the PRD/task record to "the audit-writing call site is
not deleted from the file" (true and useful, just weaker than currently stated), or (b) strengthen the guard
to check per-branch (e.g. parse each `if`/`return` early-exit and require a preceding `SaveAsync` on that
path, or require a single audit call site that dominates every return — a bigger change, likely out of scope
for a "permanent guard" task and better suited to a static-analysis tool than a string scan). Given YAGNI,
(a) — correcting the claim to match what was built — is the cheaper, honest fix; log as tech debt rather
than block on it, since the two real gaps it would have needed to catch were *already* found and filed by
hand this session.

**[Advisory] N2 — `api-contracts.md` is stale re: OpenAPI commit status**

`docs/pdlc/design/api-refactor-foundations/api-contracts.md:17`: *"there is no committed OpenAPI
specification"* — written before ADR-048 (this session) reversed that. `docs/api/openapi/*.json` are now
committed. Recommend Jarvis's doc-freshness pass at Ship correct this line; not blocking Review since the
correction belongs with the design docs, not the code.

### Over-Engineering Lens (YAGNI)

No `[Critical]`/`[Important]` over-engineering found. Specifically checked: no sixth NuGet package was added
(`OpenApiSpecGenerator` uses `ISwaggerProvider`, already in DI); `KafkaClientFake` is a single recording
class, not a mocking framework; the OpenAPI generator writes JSON with `Microsoft.OpenApi`'s own writer, not
a hand-rolled serializer. One `yagni:` note, non-blocking: `ConfiguredCollection.Of<T>` (F-018-T12) and the
Support/Contract/Persistence/Audit/OpenApi folder split add a small amount of structure ahead of F-019's
actual need — reasonable given F-019 is the very next feature and will use the same harness, so this is
provisioning for a known near-term consumer, not speculative.

### Second Pass

1. **AC coverage per-criterion** — done in `verification.md` (T20), independently re-walked by Echo below; not re-duplicated here.
2. **Authorization checked at the owning layer** — N/A, F-018 adds no new authenticated route; it tests existing ones.
3. **Cross-cutting concern applied consistently** — `configureServices`/`Services` additions to `ServiceHostFixture` (T10, T12) are consistent: both are additive optional/new members, checked against all 103 existing call sites in `BLAST-RADIUS_*.md` — pass.
4. **Reversibility** — the one production change (`AddProviderCommandHandler`'s constructor: `KafkaClient` → `IKafkaClient`) is fully reversible (widening a parameter type, no data model involved) — pass, no foundation-crack risk.
5. **Test existence ≠ coverage of the change** — checked `KafkaClientFakeProviderRegistrationTest.cs`: it exercises the *new* behavior (the interface substitution actually working), not a pre-existing passing test being pointed at post-hoc — pass.

### Files Verified

`AgendaBuddy.IntegrationTests/Audit/EventStoreWriteGuardTest.cs`, `EventAndCommands/Commands/Services/UpdateServicesFromProviderCommandHandler.cs`, `docs/pdlc/design/api-refactor-foundations/api-contracts.md`, `AgendaBuddy.IntegrationTests/Harness/ServiceHostFixture.cs`, `Provider/Requests/RequestCollection.cs`, `EventAndCommands/Commands/Provider/AddProviderCommandHandler.cs`, `AgendaBuddy.IntegrationTests/Support/KafkaClientFakeProviderRegistrationTest.cs`, `AgendaBuddy.IntegrationTests/OpenApi/OpenApiSpecGenerator.cs`.

### Suspicions Refuted

- *"The `.editorconfig` reformat (168 files) might have changed behavior somewhere."* Refuted by `git diff --numstat` on the reformat commit (`accae1e`): every file is exactly 1 insertion/1 deletion (a missing trailing newline), confirmed in `verification.md` §1 and by this session's own 484/484 + 301/301 re-runs after the merge.
- *"`ServiceHostFixture`'s two additive changes (T10, T12) might conflict."* Refuted by reading the merge (`git show` on the merge commit): both touched disjoint regions (a new optional parameter, a new property) and auto-merged cleanly.

### Verified Strengths

- `KafkaClientFakeProviderRegistrationTest.cs` doesn't just check "no exception" — it asserts the fake recorded `KafkaHelper.CreateProviderTopicName(email)` specifically, which is the actual convention CONSTITUTION §3 protects, not a weaker "something happened" check.
- `OpenApiSpecGeneratorTest.cs`'s AC-18 case triggers a *real* boot failure (a malformed `JWT_PUBLIC_KEY` causing `AuthenticationExtensions`'s DI registration to throw) rather than a simulated/mocked one — this is exactly the "reasoned, not observed" discipline this project's own episode 001 demands, applied correctly.
- `MongoFailClosedTest`/`MongoEndpointGuardTest` (F-016, re-verified this session) never echo the rejected connection string into an error message — checked `T002_NeverEchoesACredentialIntoItsOwnErrorMessage`, confirmed it exists and passes.

---

## Phantom's Findings — Security

**OWASP Top 10 sweep:** Pass — no new endpoint, no new input-validation surface; F-018 is a test-harness-completion feature, not an endpoint feature.
**Auth & session security:** Pass — no change to authentication/authorization logic.
**Input validation:** N/A — no new user-facing input path.
**Secrets & credential handling:** Pass, verified (see threat-mitigation check below).

### Threat-Model Mitigation Check (issue #55)

`threat-model.md` names 3 "mitigate now" threats: T-001, T-002, T-004 (T-003 resolved via the ADR-020 middle
path, T-005/T-006/T-007 accepted/deferred with ADRs). All three trace to security-tagged ACs on F-018-T06/T08,
linked to F-016's real tests when F-018 resumed:

| Threat | AC | Linked test | Confirmed passing |
|---|---|---|---|
| T-001 | AC-28 | `Harness/MongoEndpointGuardTest.T002_RejectsAnEndpointThatIsNotTheFixturesOwnContainer` | ✅ — ran in this session's 301/301 full suite pass |
| T-004 | AC-29 | `Harness/MongoFailClosedTest.T002_AbortsDuringFixtureConstruction_AndCreatesNoDatabase` | ✅ — same run |
| T-002 | AC-30 | `Library.Tests/Security/KeyMaterialHygieneTest.NoTrackedFile_ContainsPemKeyMaterial` | ✅ — ran in this session's 484/484 full suite pass |

`node scripts/tasks.cjs check --json` run directly: **zero `security-ac-untested` findings for any F-018
task.** The two `security-ac-untested` warnings the tool does report (`F-017-T05`, `F-017-T09`) are
pre-existing, unrelated to F-018, and out of this review's scope.

**AC-31's convention guard (not a modeled threat, CONSTITUTION §3):** `Support/KafkaClientFakeProviderRegistrationTest.cs`
exists and passes — confirmed above under Neo's Verified Strengths.

### Findings

**[Advisory] P1 — the `KafkaClientFake` is test-only and cannot leak into production, but confirm the guard**

`docs/pdlc/tasks/F-018/F-018-T06.md`'s AC1 asserts "no production csproj references
AgendaBuddy.IntegrationTests" — the same shape of guard as T-002's mitigation. Confirmed this guard covers
the new `Support/` folder too (it's a project-reference check, not folder-scoped) by reading
`KeyMaterialHygieneTest.NoProductionProject_TakesAProjectReferenceOnTheIntegrationHarness` — it walks all
`.csproj` files project-wide, not a fixed list. No new production dependency was introduced. No action needed;
recorded as a verified strength, not a gap.

**No Critical or Important security findings.**

### Phantom's security sign-off

✓ No critical or important security issues found. All three "mitigate now" threats' mitigations are
implemented in code and asserted by a linked, passing test — not a citation standing in for a control.

---

## Echo's Findings — Test Coverage & Quality

**Acceptance criteria coverage:** 29 of 31 covered (AC-11, AC-14 confirmed not built — independently re-verified below, matches `verification.md`'s own disclosure).
**Unit test coverage:** Adequate.
**Integration test coverage:** Adequate — Tier 1/2/3 now exist for the first time.
**Edge cases tested:** Partial — see E1.

### AC-by-AC walk (independent re-verification of `verification.md`'s table)

Spot-checked 8 of 31 (the ones most likely to be wrong: newly-built-this-session, or where "F-016 already
did it" is the claim) rather than re-deriving all 31 from scratch, per the instruction to verify rather than
re-derive:

| AC | Claimed test | Verified? |
|---|---|---|
| 5 (Tier 1) | `Contract/*.cs`, 7 files | ✅ — read `BookingRouteContractTest.cs`, `IdentityRouteContractTest.cs`; both assert status code only, matching the design constraint, not envelope shape |
| 8 (Identity tier 1+2) | `Contract/IdentityRouteContractTest.cs` + `Persistence/IdentityPersistenceTest.cs` | ✅ — `IdentityPersistenceTest.cs` chains register→login→refresh→logout in one test, confirmed by reading it; covers all 5 routes including `/device-token` in a second test |
| 15 (permanent guard) | `Audit/EventStoreWriteGuardTest.cs` | ✅ exists and passes, **but see Neo's N1 — I agree with and cross-reference his finding, same root cause** |
| 19 (drift check) | `OpenApi/OpenApiSpecDriftTest.cs` | ✅ — confirmed the red case is a *real* regeneration diff (renamed operation ID), not a mocked failure |
| 26 (`.editorconfig` + format CI) | `.editorconfig` + `dotnet.yml` | ✅ — confirmed the CI step uses `--no-restore` (reusing the earlier restore step), not a redundant one |
| 28/29/30 (security) | see Phantom's table above | ✅ — cross-checked independently, same conclusion |
| 11 (image-pull diagnostics) | none | ✅ **confirmed not built** — `grep -rn "image.*pull\|pull.*failure\|ImagePull" AgendaBuddy.IntegrationTests/` finds nothing real |
| 14 (AppHost-running warning) | none | ✅ **confirmed not built** — `grep -rn "already running\|AppHost.*running" AgendaBuddy.IntegrationTests/` finds nothing real |

No disagreement with `verification.md`'s self-assessment on any of the 8 spot-checked.

### Findings

**[Important] E1 — `EventStoreWriteGuardTest` doesn't test per-branch coverage (linked with Neo's N1 — same root cause)**

Same finding as Neo's N1, from the coverage angle: the theory is parametrized over *files*, not over
*handler branches*. A handler with 3 return paths and 1 audit call anywhere in the file passes the same as a
handler with 3 return paths and 3 audit calls. This is a real coverage gap in the guard itself, not just an
architectural framing issue — recommend a single fix resolves both: either reword AC-15's claim (Neo's
option a) or add branch-level assertions (Neo's option b). **Primary finding: N1** (Neo's — his framing names
the concrete missed defect); this entry is the coverage half of the same root cause.

**[Advisory] E2 — no test exercises `OpenApiSpecGenerator`'s Profession-specific unreachable-Mongo workaround directly**

`OpenApiSpecGenerator.cs` supplies every service an unreachable-but-syntactically-valid Mongo connection
string, needed only because `Profession`'s `ProfessionSeedHostedService` resolves `IMongoClient` at startup.
The 7-service theory test (`OpenApiSpecGeneratorTest.cs`) exercises this for all 7 including Profession, so
it IS covered — but there's no test asserting specifically that removing the workaround would make
Profession's case fail (i.e., no test isolates *why* Profession needs it, only that all 7 currently pass).
Low value to add given YAGNI — the 7-service theory already catches a regression, just without naming which
service would break. Not blocking.

### Files Verified

`Contract/BookingRouteContractTest.cs`, `Contract/IdentityRouteContractTest.cs`, `Persistence/IdentityPersistenceTest.cs`, `Audit/EventStoreWriteGuardTest.cs`, `OpenApi/OpenApiSpecDriftTest.cs`, `.github/workflows/dotnet.yml`, `OpenApi/OpenApiSpecGenerator.cs`, `OpenApi/OpenApiSpecGeneratorTest.cs`.

---

## Jarvis's Findings — Documentation Completeness

**Inline code documentation:** Complete — every new class in `AgendaBuddy.IntegrationTests/{Support,Contract,Persistence,Audit,OpenApi}/` carries an XML `<summary>`/`<remarks>` block explaining the *why*, not just the *what* (spot-checked `EventStoreWriteGuardTest.cs`, `KafkaClientFake.cs`, `OpenApiSpecGenerator.cs` — all three explain a non-obvious constraint, not restate the method name).
**API documentation:** Gap found (see J1).
**CHANGELOG entry drafted:** Yes — see below.
**README updated (if needed):** N/A — no developer-workflow change (`.editorconfig`/CI format-gate is covered by `CLAUDE.md`'s Format line, already updated this session by T19).

### Findings

**[Advisory] J1 — `api-contracts.md` OpenAPI section is stale (same finding as Neo's N2, not duplicated as a separate root cause — cross-referenced)**

Linked with **Neo's N2**. Same file, same line, same fix (a Ship-time doc-freshness pass). Filing once here
for Jarvis's documentation-completeness ownership, cross-referenced rather than re-analyzed.

**[Advisory] J2 — `CLAUDE.md`'s Key Files section for `AgendaBuddy.IntegrationTests/` is comprehensive but long**

The entry for `AgendaBuddy.IntegrationTests/` (updated this session, F-018-T19) now runs to 5 sub-clauses
covering Support/Contract/Persistence/Audit/OpenApi. Accurate and each clause earns its place, but it's
approaching the length where a reader skims past it. Non-blocking style note — a future doc-freshness pass
could extract it to a dedicated `docs/pdlc/context/` testing-conventions note if it grows further; not worth
doing now for 5 clauses.

**No Important or Critical documentation findings.**

### Draft CHANGELOG entry

```markdown
## [Unreleased] — api-refactor-foundations (F-018)

### Added
- Testcontainers integration-test harness completion: Tier 1 (route-contract), Tier 2 (persistence
  round-trip), and Tier 3 (audit-fired) test coverage across all 7 services, plus a permanent structural
  guard proving CONSTITUTION §3's audit-trail invariant isn't silently removable.
- `KafkaClientFake` — a recording `IKafkaClient` substitute, so the per-provider-topic convention stays
  test-guarded without a Kafka container.
- Byte-deterministic OpenAPI spec generation via `ISwaggerProvider` (no HTTP call, no container) — specs are
  now committed to `docs/api/openapi/*.json` and drift-checked in CI (ADR-048 supersedes ADR-020's earlier
  commit deferral, now that F-016 has closed the anonymous-PII exposure that deferral protected against).
- `.editorconfig` at the repo root, enforced in CI via `dotnet format --verify-no-changes`.
- `scripts/verify-container-reaping.sh` — proves Testcontainers' Ryuk reaper actually cleans up orphan
  containers after a mid-flight kill (observed via a real SIGKILL, not assumed from documentation).

### Fixed
- `Provider/Requests/RequestCollection.cs`'s `(kafkaClient as KafkaClient)!` downcast silently produced a
  `null` reference — and a `NullReferenceException` — the moment `IKafkaClient` was substituted with
  anything but the concrete class. `AddProviderCommandHandler` now depends on the interface.
- `EventAndCommands/Program.cs`... *(no change — Identity's stale comment claiming a shared EventStore was
  corrected; Identity registers none)*.

### Changed
- CONSTITUTION §1 (net10.0, not .NET 8), §4 (records the `Validot` migration target per ADR-016 without
  claiming it's already the code), §9 (records ADR-015's five pre-approved packages).

### Known issues (filed, not fixed in this release)
- `Booking`/`Customer`'s `RequestCollection.cs` carry the same dormant `IKafkaClient` downcast Provider had —
  latent, not yet exercised (`agenda-buddy-5og`).
- `UpdateCustomerCommandHandler` audits its failure branch under the wrong event `Type` (`agenda-buddy-id4`).
- `UpdateServicesFromProviderCommandHandler` writes no audit event on its provider-not-found branch
  (`agenda-buddy-f49`).
- AC-11 (image-pull-failure diagnostics) and AC-14 (AppHost-already-running warning) were never built,
  despite an earlier task-store note crediting them as delivered (`agenda-buddy-10g`).
```

---

## Summary & Overall Recommendation

**Overall recommendation:** Approve with conditions (none are Critical — see below).

**Blocking issues (must fix before shipping):** None.

**Recommended fixes (strong advice):**
- **N1/E1** (linked, same root cause) — `EventStoreWriteGuardTest`'s guard proves less than AC-15 claims; recommend narrowing the claim in `docs/pdlc/tasks/F-018/F-018-T13.md`'s AC-15 note to describe what's actually built, since the concrete gap it would need to catch was already found and filed by hand this session.

**Deferred items (accepted for now, already filed as beads issues during Build — not new):**
- `agenda-buddy-5og` (dormant Booking/Customer downcast)
- `agenda-buddy-id4` (wrong audit `Type`)
- `agenda-buddy-f49` (missing failure-path audit)
- `agenda-buddy-10g` (AC-11/AC-14 never built)
- **N2/J1** (linked) — `api-contracts.md`'s stale OpenAPI-commit-status line, fix at Ship's doc-freshness pass
- **E2** — no isolating test for Profession's unreachable-Mongo workaround, low value under YAGNI

---

## Human Decision

**Decision:** <!-- pending -->

**Conditions / notes from human:**

**Reviewed by:** <!-- pending -->
**Date of decision:** <!-- pending -->

---

## PR Comments

**Pushed to PR:** no
**PR link:** https://github.com/ogdevlabs/agenda-buddy/pull/69
