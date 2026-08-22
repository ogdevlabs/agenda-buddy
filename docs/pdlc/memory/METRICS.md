# Metrics
<!-- pdlc-template-version: 2.4.0 -->
<!-- Append-only. Jarvis adds a row after every shipped feature.
     Used by Atlas for roadmap planning and by Doctor for trend analysis.
     Do not delete rows — they form the project's delivery history. -->

**Project:** Agenda Buddy
**Last updated:** 2026-08-22

---

## Delivery Metrics

| Episode | Feature | Type | Cycle Days | Test Pass % | Review Rounds | Strikes | Tier1 Overrides | Security Findings | Tasks | Date Shipped |
|---------|---------|------|-----------|-------------|---------------|---------|-----------------|-------------------|-------|-------------|
| 001 | aspire-wiring | Feature | 3 | 100 | 1 | 0 | 0 | 2 | 14 | 2026-08-18 |
| 002 | secure-public-endpoints | Feature | 1 | 100 | 1 | 0 | 0 | 2 | 20 | 2026-08-18 |

*Notes on episode 002:* **Cycle days = 1** — roadmap claim 2026-08-18T17:52Z → merged and tagged the same day. The **ship gate then stayed open four days**, closing 2026-08-22; that lag is not in the cycle-day figure and is the episode's main process finding. **Test pass % = 100** over **531** tests (358 backend + 99 integration + 74 mobile, 7 of them skipped), re-verified green on `main` at the ship gate. **Review rounds = 1** (fix cycle 1 of 3: I-3 and I-4 fixed, I-1/I-2/I-5 accepted). **Overrides = 0** — four guardrail *warnings* were logged (standards gate unavailable, test layers 3–6 absent, the ADR-030 SSH.NET HIGH, §7 satisfied by hand), none of which is an override ceremony. **Security findings = 2** — both Phantom Importants: the unprojected providers-list cache (I-1) and the full-`CustomerEntity` payload to any Provider-role caller (I-2). **Tasks = 20**, of which **8 were absorbed from F-018's approved plan** to build the integration harness first.

*Notes on episode 001:* **Cycle days** = roadmap claim 2026-08-15 → shipped 2026-08-18. **Test pass %** = 305/305 with 0 warnings (baseline before the feature was 189 across 10 projects). **Review rounds** counts 1 formal round, but that undercounts what happened — a late single-reviewer report (Echo) arrived *after* the approval gate and reopened it with a Critical. **Tier1 Overrides = 0**: no `/override` was invoked; two guardrail *warnings* were logged at ship (phase-marker mismatch, and the §7 security-scan gate being unimplemented), which are logged warnings, not override ceremonies. **Security findings = 2**: 1 Critical (C-1, PII in exported spans — Echo) + 1 Important (I-1, CI credential guard exempted `docs/pdlc` — Phantom).

---

## Trend Summary

**Last updated:** 2026-08-22 (after Episode 002: secure-public-endpoints)

### This episode vs project average

| Metric | This Episode | Project Avg | Trend |
|--------|-------------|-------------|-------|
| Cycle time | 1 day | 2 days | ↓ faster |
| Test pass rate | 100% | 100% | → same |
| Review rounds | 1 | 1 | → same |
| Strike escalations | 0 | 0 | → same |
| Security findings | 2 | 2 | → same |

### This episode vs previous (aspire-wiring)

| Metric | This Episode | Previous | Change |
|--------|-------------|----------|--------|
| Cycle time | 1 day | 3 days | −2 |
| Test pass rate | 100% | 100% | same |
| Review rounds | 1 | 1 | same |
| Tests in suite | 531 | 379 | +152 |
| Tasks | 20 | 14 | +6 |

### Observations

1. **The two-episode sample is too small for trends, and the numbers hide the interesting part.** Both episodes report 100% pass and 1 review round. What actually differs is that 002 delivered 6 more tasks in a third of the cycle time — because 8 of its 20 tasks came from F-018's *already-approved* plan, so the planning cost had been paid in a prior feature.
2. **The pattern from 001 repeated exactly: both features' real defects were found by running the software, not by reviewing it.** 001 found six services unable to start in `Development` and a per-request Mongo connection pool; 002 found, at the ship gate, that no cache invalidation exists anywhere in the solution (`agenda-buddy-xrw`) — a provider who finishes onboarding is missing from discovery for five minutes. Both times the pass rate was 100% before and after. **Any AC whose evidence is "code review" should be treated as unverified.**
3. **Cycle time is now a misleading metric for this project.** 002's build took one day; its *ship gate* took four more, with the tag pushed and the memory files sitting uncommitted in a working tree the whole time. A cycle-days column that measures claim→merge will keep reporting improvement while the gate lag grows. Worth adding a gate-lag column if it happens a third time.
4. **The standards-readiness gate has now been skipped five consecutive times and has never executed.** Not a delivery metric, but the most persistent process signal in the project: a gate marked `enforcing` that has never run.

---

## UX Scorecard Trend

| Episode | Feature | Triage | Nielsen (d / a / s) | Audit-5d (d / a / s) | Cognitive load failures (d / a / s) | Findings P0 / P1 / P2 / P3 | ADRs open / closed | Date Shipped |
|---------|---------|--------|---------------------|----------------------|--------------------------------------|---------------------------|--------------------|-------------|
<!-- No UX-audited features yet. -->

*Legend:* `d` = design-time (Step 10.6), `a` = as-built (Construction Review), `s` = ship-verify. P0 finding count should always be `0` at ship — P0 blocks merge unless `/pdlc override` was invoked.

### UX trend signals

Latest UX trend: <!-- Populated by Jarvis after every UX-audited ship. -->

---

## Variant Convergence calibration log

| Date | Feature | Step | Gate outcome | Trigger signal | Variants generated | Convergence outcome | Useful? (Y/N) | Note |
|------|---------|------|--------------|----------------|--------------------|---------------------|---------------|------|
<!-- No fires yet. -->

### Calibration trigger

When this log accumulates **3 fires**, Jarvis flags it at the next Reflect with a recommendation to review the `Useful?` distribution and re-tune thresholds in `skills/variant-convergence/SKILL.md`.

---

## Readiness Trend
<!-- The planning-quality feedback loop. One append-only row per feature.
     - The PLANNING-TIME columns (Readiness, Gaps@plan) are written by the PRD +
       Plan Readiness Party at Brainstorm Step 18.6 (skills/brainstorm/steps/readiness-party.md).
     - The SURFACED-LATER + Delta columns are filled by Jarvis at Ship Reflect
       Step 16g, reconciling what the plan predicted against the planning gaps
       that actually leaked into Build/Ship.
     Do not delete rows — the misses (gaps that surfaced but weren't flagged at
     planning) accumulate into the framework-improvement signal.
     See docs/wiki/27-metrics.md for the full picture and the cross-repo aggregation intent. -->

| Feature | Name | Triage | Readiness (overall + per-dim) | Gaps@plan (by category) | Gaps@surfaced-later (by category) | Planning-accuracy delta | Taxonomy ver | Date |
|---------|------|--------|-------------------------------|-------------------------|-----------------------------------|-------------------------|--------------|------|
| F-016 | secure-public-endpoints | Full | **Fair** — Completeness Fair · Traceability Fair · Durability Fair | **4** — `ac-contradicts-adr` ×1 *(corrected in-party: AC-12 required a 403 on a route ADR-025 deletes)* · `verification-unenforced` ×1 *(RESOLVED at the Step 18 gate — F-018 T18 absorbed as F-016-T20)* · `tooling-scope-leak` ×1 *(`tasks.cjs ready` returns paused-F-018 tasks)* · `requirement-without-dedicated-ac` ×2 *(reqs 8, 20 — distributed coverage)* | `ac-uncovered` ×1 *(AC-14 verified on 1 of 6 catch sites — I-3)* · `nfr-underspecified` ×1 *(no cache-invalidation requirement anywhere, surfaced at Verify — `agenda-buddy-xrw`)* · **`stale-context-propagated` ×3 — NOT a v2 category; proposed here** *(9-vs-10 handlers, 7-vs-8 catch sites, two non-existent entity fields; all three originated in the context catalog and reached approved artifacts)* | misses: `nfr-underspecified`:1, *(proposed)* `stale-context-propagated`:3; known: `ac-uncovered`:1; caught: 3 *(`ac-contradicts-adr`, `verification-unenforced`, `tooling-scope-leak` all closed before Build)* | v2 | 2026-08-22 |
| F-013 | aspire-wiring | Skip | n/a | — | security-ac-unmaterialized:1, ac-uncovered:1, nfr-underspecified:2, dependency-missed:2, estimate-mis-scoped:1 | no-baseline (no Readiness Party row at plan) | v2 | 2026-08-18 |
| F-018 | api-refactor-foundations | Full | Fair (C:Fair T:Fair D:Fair) | ac-uncovered:1, task-orphan:1, dependency-missed:1 | *(pending Ship Reflect 16g)* | *(pending)* | v2 | 2026-08-18 |

> **Proposed taxonomy addition — `stale-context-propagated`.** F-016's three counting errors share a cause the
> D7 v2 categories do not name: a wrong fact in `docs/pdlc/context/` was copied into a PRD, a design doc, a
> plan and a task body, passing three approval gates on the way. It is not `requirement-missing` (the
> requirement was there and correct in intent) nor `ac-uncovered` (the ACs were covered). All three were
> caught by `grep` during Build, and one of them — `15-cqrs-and-messaging.md`'s "10 queries, 10 handlers"
> above a 9-row table — was still unfixed at ship and is only corrected now. If this recurs in F-021 or
> F-014, it should become a real category, because the mitigation is specific: **verify catalog counts
> against code at the Define gate, not at the wave standup.**

**Attribution for F-013's surfaced-later gaps** — recorded so a later reader can judge the classification rather than trust it:

| Category | What actually surfaced |
|---|---|
| `security-ac-unmaterialized` ×1 | Threat **T-004** (PII in exported spans) was marked mitigated by *citation* — "instrumentation records `http.route` templates" — with no asserting test. When the test was finally written it **failed**: `url.path` was exporting real customer email addresses. This is precisely the category `v2` was added for. |
| `ac-uncovered` ×1 | **AC-2.1** was self-defeating as written: it embedded the password it forbade, guaranteeing a permanent `git grep` match. An AC that can never pass is an uncovered AC. |
| `nfr-underspecified` ×2 | **AC-1.4** assumed dynamic ports come free; Aspire pins them by *two* independent routes (launch profile *and* `Kestrel:Endpoints`), so fixing one left a service on a fixed port. And no integration-test harness was planned, so orchestrated-startup regressions remain uncatchable in CI. |
| `dependency-missed` ×2 | **T-14** split off because the AppHost end-to-end run was unproven — the plan assumed a working container runtime and a resolvable environment. And the new CI startup guard consumed `secrets.CI_JWT_*` that were never created, so it first ran, and first failed, on PR #35. |
| `estimate-mis-scoped` ×1 | The plan's claim that "the existing tests keep compiling" was false: the coupling was the **primary constructor**, not the interface everyone was inspecting, so three test files broke. |

**No planning-time baseline exists** for F-013 — its Inception predates the Readiness Party step, so there is nothing to score these against. The row is `no-baseline` rather than a set of misses; treating unflagged gaps as blind spots would be unfair when no gate ran. F-018 will be the first feature with a real baseline, at which point the misses column becomes meaningful.
<!-- No features yet. Format examples (replace when the first Readiness Party runs):
| F-002 | user-auth | Full | Fair (C:Strong T:Fair D:Strong) | ac-uncovered:1, task-orphan:1 | dependency-missed:1, ac-uncovered:1 | misses: dependency-missed:1; known: ac-uncovered:1; caught: 1 | v1 | 2026-05-15 |
| F-003 | csv-export | Lite | Strong (C:Strong T:Strong D:Strong) | — | — | misses: 0; known: 0; caught: 0 | v1 | 2026-05-22 |
| F-004 | webhook-retry | Skip | n/a | — | nfr-underspecified:1 | no-baseline (party skipped at plan) | v1 | 2026-06-01 |
-->

*Legend:* `Triage` = `Skip` / `Lite` / `Full` (from Step 18.6). `Readiness` = overall tier (Strong/Fair/Weak) with per-dimension in parens: `C`=Completeness, `T`=Traceability, `D`=Durability. `Gaps@plan` = categories the party flagged at planning; `Gaps@surfaced-later` = categories that actually surfaced in Build/Ship (from split-and-defer, `/decide` scope-creep, review findings, tech debt). `Planning-accuracy delta`: **misses** = surfaced-later but NOT flagged at plan (blind spots — the key signal); **known** = flagged and surfaced; **caught** = flagged and prevented. `no-baseline` = no planning-time row existed (feature predates this feature, or party skipped/degraded). `Taxonomy ver` pins the gap-category set (currently `v2`) so cross-repo aggregation reconciles category changes.

**Gap-category taxonomy (`v2`):** `requirement-missing` · `ac-uncovered` · `task-orphan` · `nfr-underspecified` · `dependency-missed` · `error-path-missing` · `design-finding-unlinked` · `security-ac-unmaterialized` · `estimate-mis-scoped`. Defined in `skills/brainstorm/steps/readiness-party.md` and mirrored in `docs/wiki/27-metrics.md`. (`v2` adds `security-ac-unmaterialized` for issue #55.)

---

### Recurrence signal
When the same gap category appears as a **miss** in **3 of the last 4** rows, Jarvis surfaces it at Reflect as a framework-improvement signal (e.g. *"`nfr-underspecified` surfaced post-plan in 3 of the last 4 features — planning is systematically under-specifying NFRs"*). That's the cue to strengthen the relevant planning step (Discover/Define questioning, or a new check in the Readiness Party). The **misses** are the measure of brainstorm effectiveness: fewer misses over time = planning is improving.
