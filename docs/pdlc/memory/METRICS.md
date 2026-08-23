# Metrics
<!-- pdlc-template-version: 2.4.0 -->
<!-- Append-only. Jarvis adds a row after every shipped feature.
     Used by Atlas for roadmap planning and by Doctor for trend analysis.
     Do not delete rows — they form the project's delivery history. -->

**Project:** Agenda Buddy
**Last updated:** 2026-08-23

---

## Delivery Metrics

| Episode | Feature | Type | Cycle Days | Test Pass % | Review Rounds | Strikes | Tier1 Overrides | Security Findings | Tasks | Date Shipped |
|---------|---------|------|-----------|-------------|---------------|---------|-----------------|-------------------|-------|-------------|
| 001 | aspire-wiring | Feature | 3 | 100 | 1 | 0 | 0 | 2 | 14 | 2026-08-18 |
| 002 | secure-public-endpoints | Feature | 1 | 100 | 1 | 0 | 0 | 2 | 20 | 2026-08-18 |
| 003 | identity-hardening | Feature | 1 | 100 | 0 | 0 | 0 | 0 | 7 | 2026-08-22 |

*Notes on episode 003:* **Cycle days = 1** — roadmap claim 2026-08-22T15:35Z, merged and tagged the same day; and unlike 002 the **ship gate did not lag** (build complete → verified → tagged in one session, so the process finding recorded at 002 did not repeat). **Test pass % = 100** over **623** tests (431 backend + 118 integration + 74 mobile, 7 skipped). **Review rounds = 0** — no formal party review ran: the session carried a standing instruction not to spawn agents, so review was solo and inline. That is a **gap in this row, not an achievement**; a 0 here should be read as "not independently reviewed", which is exactly the fidelity caveat the threat model records. **Security findings = 0** *from review*, which for the same reason measures nothing — the security work is in the other direction: five threats (T-101…T-105) were mitigated and one accepted, all six with their decisions recorded. **Tasks = 7**, the smallest task count of any episode so far, for a feature that changed all seven services — because 5 of the 7 tasks are single-concern and the transport change was one shared extension plus seven one-line call sites rather than seven edits. **Three defects were found by *running* the software, not by reviewing it**, continuing the pattern from 001 and 002: a rejected refresh answering 500 instead of 401 (caught by the integration harness, invisible to every unit test), the first AC-16 live check being vacuous because Aspire streams service logs to the dashboard rather than stdout, and three pre-existing "sanitization" tests that had never asserted anything.

*Notes on episode 002:* **Cycle days = 1** — roadmap claim 2026-08-18T17:52Z → merged and tagged the same day. The **ship gate then stayed open four days**, closing 2026-08-22; that lag is not in the cycle-day figure and is the episode's main process finding. **Test pass % = 100** over **531** tests (358 backend + 99 integration + 74 mobile, 7 of them skipped), re-verified green on `main` at the ship gate. **Review rounds = 1** (fix cycle 1 of 3: I-3 and I-4 fixed, I-1/I-2/I-5 accepted). **Overrides = 0** — four guardrail *warnings* were logged (standards gate unavailable, test layers 3–6 absent, the ADR-030 SSH.NET HIGH, §7 satisfied by hand), none of which is an override ceremony. **Security findings = 2** — both Phantom Importants: the unprojected providers-list cache (I-1) and the full-`CustomerEntity` payload to any Provider-role caller (I-2). **Tasks = 20**, of which **8 were absorbed from F-018's approved plan** to build the integration harness first.

*Notes on episode 001:* **Cycle days** = roadmap claim 2026-08-15 → shipped 2026-08-18. **Test pass %** = 305/305 with 0 warnings (baseline before the feature was 189 across 10 projects). **Review rounds** counts 1 formal round, but that undercounts what happened — a late single-reviewer report (Echo) arrived *after* the approval gate and reopened it with a Critical. **Tier1 Overrides = 0**: no `/override` was invoked; two guardrail *warnings* were logged at ship (phase-marker mismatch, and the §7 security-scan gate being unimplemented), which are logged warnings, not override ceremonies. **Security findings = 2**: 1 Critical (C-1, PII in exported spans — Echo) + 1 Important (I-1, CI credential guard exempted `docs/pdlc` — Phantom).

---

## Trend Summary

**Last updated:** 2026-08-22 (after Episode 002: secure-public-endpoints)

### This episode vs project average

| Metric | This Episode (003) | Project Avg | Trend |
|--------|-------------|-------------|-------|
| Cycle time | 1 day | 1.7 days | ↓ faster |
| Test pass rate | 100% | 100% | → same |
| Review rounds | **0** | 1 | ⚠️ **down, and that is bad** — see observation 5 |
| Strike escalations | 0 | 0 | → same |
| Security findings (from review) | 0 | 1.3 | ⚠️ measures nothing this episode — no independent review ran |

### This episode vs previous (secure-public-endpoints)

| Metric | This Episode | Previous | Change |
|--------|-------------|----------|--------|
| Cycle time | 1 day | 1 day | same |
| Ship-gate lag | **0 days** | 4 days | **−4** |
| Test pass rate | 100% | 100% | same |
| Review rounds | 0 | 1 | −1 ⚠️ |
| Tests in suite | 623 | 531 | +92 |
| Tasks | 7 | 20 | −13 |
| Threats mitigated / accepted | 5 / 1 | 7 / 1 | −2 / same |

### Observations

1. **The two-episode sample is too small for trends, and the numbers hide the interesting part.** Both episodes report 100% pass and 1 review round. What actually differs is that 002 delivered 6 more tasks in a third of the cycle time — because 8 of its 20 tasks came from F-018's *already-approved* plan, so the planning cost had been paid in a prior feature.
2. **The pattern from 001 repeated exactly: both features' real defects were found by running the software, not by reviewing it.** 001 found six services unable to start in `Development` and a per-request Mongo connection pool; 002 found, at the ship gate, that no cache invalidation exists anywhere in the solution (`agenda-buddy-xrw`) — a provider who finishes onboarding is missing from discovery for five minutes. Both times the pass rate was 100% before and after. **Any AC whose evidence is "code review" should be treated as unverified.**
3. **Cycle time is now a misleading metric for this project.** 002's build took one day; its *ship gate* took four more, with the tag pushed and the memory files sitting uncommitted in a working tree the whole time. A cycle-days column that measures claim→merge will keep reporting improvement while the gate lag grows. Worth adding a gate-lag column if it happens a third time.
4. **The standards-readiness gate has now been skipped SEVEN consecutive times and has never executed.** Not a delivery metric, but the most persistent process signal in the project: a gate marked `enforcing` that has never run once, across three shipped releases. Two more skips since this observation was written. It needs a reachable source or an explicit retirement — the recommendation has not changed since F-013 and is now the oldest unaddressed process finding here.
5. **Episode 003's `Review Rounds = 0` is the first metric in this table that is worse for being lower.** No independent review ran: the session carried a standing instruction not to spawn agents, so every "party" was one model reasoning as each role. `Security Findings = 0` in that row therefore measures the *absence of reviewers*, not the absence of defects — and the three real defects that episode found all came from running the software. **Any row with 0 review rounds should be read as "not independently reviewed" and its findings count discounted entirely.** If this happens again, the honest fix is a separate column rather than a zero that looks like a clean sheet.
6. **The ship-gate lag that observation 3 predicted "if it happens a third time" did not recur.** 002 took four days between tag and gate closure; 003 took none. One data point, but the mitigation was simply doing the memory-file writes in the same session as the build rather than leaving them in a working tree.

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
| F-021 | identity-hardening | Full | **not assessed at plan** — no Readiness Party ran (solo session, no agents) | *(no baseline row)* | `design-not-implementable` ×2 *(ARCHITECTURE §4's "under their flags" could not apply to the redirect without removing an existing control; §3.2's flow omitted that the signing key must be read before the write)* · `ac-assumes-wrong-response-shape` ×1 *(AC-7 assumed an empty 401 body; `UseStatusCodePages` makes it ProblemDetails — the same surprise F-016 hit with 403)* · `tooling-absent` ×1 *(`scripts/tasks.cjs` does not exist in this repo, so the structural security-AC-to-test check could not run and the task store is hand-written)* · `test-asserts-nothing` ×1 *(three pre-existing sanitization tests iterated a logger wired to nothing)* | **no baseline** — nothing was flagged at planning because no planning-quality gate ran. All five surfaced in Build. The `stale-context-propagated` category F-016 proposed did **not** recur; `design-not-implementable` is proposed here as a new one | v2 | 2026-08-22 |
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
