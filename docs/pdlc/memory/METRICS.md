# Metrics
<!-- pdlc-template-version: 2.4.0 -->
<!-- Append-only. Jarvis adds a row after every shipped feature.
     Used by Atlas for roadmap planning and by Doctor for trend analysis.
     Do not delete rows — they form the project's delivery history. -->

**Project:** Agenda Buddy
**Last updated:** 2026-08-24 (Episode 005)

---

## Delivery Metrics

| Episode | Feature | Type | Cycle Days | Test Pass % | Review Rounds | Strikes | Tier1 Overrides | Security Findings | Tasks | Date Shipped |
|---------|---------|------|-----------|-------------|---------------|---------|-----------------|-------------------|-------|-------------|
| 001 | aspire-wiring | Feature | 3 | 100 | 1 | 0 | 0 | 2 | 14 | 2026-08-18 |
| 002 | secure-public-endpoints | Feature | 1 | 100 | 1 | 0 | 0 | 2 | 20 | 2026-08-18 |
| 003 | identity-hardening | Feature | 1 | 100 | 0 | 0 | 0 | 0 | 7 | 2026-08-22 |
| 004 | wire-unreached-services | Feature | 1 | 100 | 0 | 0 | 0 | 0 | 9 | 2026-08-23 |
| 005 | api-gateway-and-mobile-contract | Feature | 2 | 100 | 0 | 0 | 0 | 0 | 14 | 2026-08-24 |
| 007 | api-refactor-foundations | Feature | 0 | 100 | 1 | 0 | 0 | 0 | 21 | 2026-08-26 |

*Notes on episode 005:* **Cycle days = 2** — roadmap claim 2026-08-23T14:00Z, merged and tagged 2026-08-24,
the first multi-day cycle since 003. Proportionate to this feature's size: **14 tasks / 5 waves**, the
largest task count of any shipped feature so far, and **+166 tests** (863→867 counting T14's own fix), also
the largest single-feature test delta. **Test pass % = 100** over **867** tests (468 backend + 234
integration + 165 mobile). **Review rounds = 0** — the same genuine process gap recorded at episode 004: no
Review sub-phase ran, no findings file exists. The human PR review that merged #41 stands in for it.
**Security findings = 0** *from review*, same caveat as 003/004 — it measures the absence of a review, not
a clean sheet; separately, both threat-derived security ACs (T-302, T-303) are dispositioned with linked
tests, and T-301 is accepted by ADR-040. **Tasks = 14**, the largest count so far, delivered by **real
per-task subagents in worktree-isolated waves** for the first time this project has used that execution
model — only two merge conflicts across the whole feature. **Three defects were found by running the
software or running CI for the first time**, continuing the unbroken pattern across all five episodes: the
Gateway's allowlist missing two Customer route groups (found by the closing-verification task, fixed before
Construction ended), and two CI-only defects — a `Routing` namespace collision and a missing build flag —
found only because opening PR #41 was the first time `Mobile — iOS/Android Build` and
`Integration — real services + MongoDB` had ever executed against this branch's code across its entire
14-task Construction phase. That last finding is a new, sharper instance of the pattern: it is not just that
defects are found by running software instead of reviewing it, but that an entire class of CI coverage can
go completely unexercised for a whole feature's Construction if nothing opens a PR until Ship.

*Notes on episode 004:* **Cycle days = 1** — roadmap claim 2026-08-23T04:30Z, merged and tagged the same
day, third same-day cycle in a row. **Test pass % = 100** over **701** tests (452 backend + 175 integration +
74 mobile) — the largest single-feature test delta of the four episodes (+78). **Review rounds = 0**, and
unlike 003 this is a **genuine process gap, not just the standing solo-session caveat**: no Review sub-phase
ran at all this cycle (no findings file exists), and no episode draft existed at Construction Complete either
— both drafted retroactively at the Ship gate. The human PR review that merged #40 stands in for it, but
there is no independent-pass record. **Security findings = 0** *from review*, for the same reason as 003 —
it measures the absence of a review, not a clean sheet. Separately, the feature's own security posture is
positive: 15 anonymous-access cases pass, and all threats T-201…T-208 are dispositioned (7 mitigated, 1
partially accepted). **Tasks = 9**, the second-smallest count for the largest test delta — the plan's task
boundaries lined up cleanly with the six-capability, one-dependency (server-owned status) shape of the work.
**Four defects were found by running the software**, continuing the pattern unbroken across all four
episodes: `ObjectId`'s unreadable JSON shape, a `DeactivateProviderCommandHandler` that could never have
completed, integer-only enum binding, and a telemetry test made flaky by this feature's own full-suite runs.

*Notes on episode 003:* **Cycle days = 1** — roadmap claim 2026-08-22T15:35Z, merged and tagged the same day; and unlike 002 the **ship gate did not lag** (build complete → verified → tagged in one session, so the process finding recorded at 002 did not repeat). **Test pass % = 100** over **623** tests (431 backend + 118 integration + 74 mobile, 7 skipped). **Review rounds = 0** — no formal party review ran: the session carried a standing instruction not to spawn agents, so review was solo and inline. That is a **gap in this row, not an achievement**; a 0 here should be read as "not independently reviewed", which is exactly the fidelity caveat the threat model records. **Security findings = 0** *from review*, which for the same reason measures nothing — the security work is in the other direction: five threats (T-101…T-105) were mitigated and one accepted, all six with their decisions recorded. **Tasks = 7**, the smallest task count of any episode so far, for a feature that changed all seven services — because 5 of the 7 tasks are single-concern and the transport change was one shared extension plus seven one-line call sites rather than seven edits. **Three defects were found by *running* the software, not by reviewing it**, continuing the pattern from 001 and 002: a rejected refresh answering 500 instead of 401 (caught by the integration harness, invisible to every unit test), the first AC-16 live check being vacuous because Aspire streams service logs to the dashboard rather than stdout, and three pre-existing "sanitization" tests that had never asserted anything.

*Notes on episode 002:* **Cycle days = 1** — roadmap claim 2026-08-18T17:52Z → merged and tagged the same day. The **ship gate then stayed open four days**, closing 2026-08-22; that lag is not in the cycle-day figure and is the episode's main process finding. **Test pass % = 100** over **531** tests (358 backend + 99 integration + 74 mobile, 7 of them skipped), re-verified green on `main` at the ship gate. **Review rounds = 1** (fix cycle 1 of 3: I-3 and I-4 fixed, I-1/I-2/I-5 accepted). **Overrides = 0** — four guardrail *warnings* were logged (standards gate unavailable, test layers 3–6 absent, the ADR-030 SSH.NET HIGH, §7 satisfied by hand), none of which is an override ceremony. **Security findings = 2** — both Phantom Importants: the unprojected providers-list cache (I-1) and the full-`CustomerEntity` payload to any Provider-role caller (I-2). **Tasks = 20**, of which **8 were absorbed from F-018's approved plan** to build the integration harness first.

*Notes on episode 001:* **Cycle days** = roadmap claim 2026-08-15 → shipped 2026-08-18. **Test pass %** = 305/305 with 0 warnings (baseline before the feature was 189 across 10 projects). **Review rounds** counts 1 formal round, but that undercounts what happened — a late single-reviewer report (Echo) arrived *after* the approval gate and reopened it with a Critical. **Tier1 Overrides = 0**: no `/override` was invoked; two guardrail *warnings* were logged at ship (phase-marker mismatch, and the §7 security-scan gate being unimplemented), which are logged warnings, not override ceremonies. **Security findings = 2**: 1 Critical (C-1, PII in exported spans — Echo) + 1 Important (I-1, CI credential guard exempted `docs/pdlc` — Phantom).

---

## Trend Summary

**Last updated:** 2026-08-24 (after Episode 005: api-gateway-and-mobile-contract)

### This episode vs project average

| Metric | This Episode (005) | Project Avg | Trend |
|--------|-------------|-------------|-------|
| Cycle time | 2 days | 1.6 days | ↑ slower, proportionate to size (14 tasks, +166 tests) |
| Test pass rate | 100% | 100% | → same |
| Review rounds | **0** | 0.4 | ⚠️ **third of five episodes with no Review sub-phase at all** — see observation 8 |
| Strike escalations | 0 | 0 | → same |
| Security findings (from review) | 0 | 0.8 | ⚠️ measures nothing this episode — no review ran |

### This episode vs previous (wire-unreached-services)

| Metric | This Episode | Previous | Change |
|--------|-------------|----------|--------|
| Cycle time | 2 days | 1 day | +1 |
| Test pass rate | 100% | 100% | same |
| Review rounds | 0 | 0 | same ⚠️ (see observation 8) |
| Tests in suite | 867 | 701 | +166 |
| Tasks | 14 | 9 | +5 |
| Threats mitigated / accepted | 2 mitigated / 1 accepted | 7 / 1 | fewer threats, smaller threat model |

### Observations

1. **The two-episode sample is too small for trends, and the numbers hide the interesting part.** Both episodes report 100% pass and 1 review round. What actually differs is that 002 delivered 6 more tasks in a third of the cycle time — because 8 of its 20 tasks came from F-018's *already-approved* plan, so the planning cost had been paid in a prior feature.
2. **The pattern from 001 repeated exactly: both features' real defects were found by running the software, not by reviewing it.** 001 found six services unable to start in `Development` and a per-request Mongo connection pool; 002 found, at the ship gate, that no cache invalidation exists anywhere in the solution (`agenda-buddy-xrw`) — a provider who finishes onboarding is missing from discovery for five minutes. Both times the pass rate was 100% before and after. **Any AC whose evidence is "code review" should be treated as unverified.**
3. **Cycle time is now a misleading metric for this project.** 002's build took one day; its *ship gate* took four more, with the tag pushed and the memory files sitting uncommitted in a working tree the whole time. A cycle-days column that measures claim→merge will keep reporting improvement while the gate lag grows. Worth adding a gate-lag column if it happens a third time.
4. **The standards-readiness gate has now been skipped EIGHT consecutive times and has never executed.** Not a delivery metric, but the most persistent process signal in the project: a gate marked `enforcing` that has never run once, across four shipped releases. It needs a reachable source or an explicit retirement — the recommendation has not changed since F-013 and is now the oldest unaddressed process finding here.
5. **Episode 003's `Review Rounds = 0` was the first metric worse for being lower** — no independent review ran because the session carried a standing instruction not to spawn agents. `Security Findings = 0` there measured the *absence of reviewers*, not the absence of defects.
6. **The ship-gate lag that observation 3 predicted "if it happens a third time" has not recurred in 003, 004, or 005.** All three closed same-day or within a day of merge. The mitigation — doing the memory-file writes in the same session as the build — is holding.
7. **Episode 004's `Review Rounds = 0` is a different, worse case than 003's.** 003's zero measured *reviewer independence* (solo session, but a review still happened, inline). 004's zero measures that **no Review sub-phase ran at all** — Construction went from `build_complete` straight to the human PR review that merged #40, with no findings file and no episode draft until the Ship gate drafted one retroactively.
8. **Episode 005 makes it three of five episodes with `Review Rounds = 0`, and the last two (004, 005) are the same case: no Review sub-phase ran at all, not just a solo one.** That is now a **recurring pattern, not a one-off** — the recurrence signal this project's own framework watches for (§Recurrence signal below). Episode 005 also surfaced a **new, sharper variant of the same root cause**: `Mobile — iOS/Android Build` and `Integration — real services + MongoDB` only trigger on push/PR to `main`, so a whole 14-task Construction phase produced two real, CI-breaking defects that sat undetected until the Ship-gate PR was the first thing to actually run those jobs. **Recommendation, now overdue across two features:** open the feature PR as a draft at Construction start, not at Ship — this would have caught both episode 005 findings mid-build instead of at the gate, and restores an approximation of the missing Review sub-phase's independent-pass value for free.

---

## UX Scorecard Trend

| Episode | Feature | Triage | Nielsen (d / a / s) | Audit-5d (d / a / s) | Cognitive load failures (d / a / s) | Findings P0 / P1 / P2 / P3 | ADRs open / closed | Date Shipped |
|---------|---------|--------|---------------------|----------------------|--------------------------------------|---------------------------|--------------------|-------------|
| 005 | api-gateway-and-mobile-contract | Lite | N/A / N/A / N/A *(no heuristic scorecard at Lite depth)* | N/A / N/A / N/A | N/A / N/A / N/A | 0 / 0 / 2 / 2 | 0 / 0 | 2026-08-24 |

*Legend:* `d` = design-time (Step 10.6), `a` = as-built (Construction Review), `s` = ship-verify. P0 finding count should always be `0` at ship — P0 blocks merge unless `/pdlc override` was invoked.

### UX trend signals

Latest UX trend: **First populated row (episode 005).** Lite triage produces no Nielsen/Audit-5d numbers to
trend against — those columns need at least one Full-triage episode before any of the four UX trend signals
(cross-feature Nielsen direction, design-time→as-built delta, as-built→ship-verify delta, open-finding
accumulation) are computable. All 4 findings were tagged fix-now and closed by construction; 0 P0/P1
findings and 0 open UX ADRs, so there is nothing accumulating to flag yet.

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
| F-014 | wire-unreached-services | *(program-level Discover, not a per-feature brainstorm)* | **not assessed at plan** — no Readiness Party ran (claimed as the anchor of a program-level Discover across F-014–F-017, not a standalone `/brainstorm`) | *(no baseline row)* | `tooling-absent` ×1 *(`scripts/tasks.cjs` still does not exist; hand-written task store, recurring for the second consecutive feature)* · `review-not-run` ×1 *(proposed — no Review sub-phase ran at all this cycle, distinct from F-021's solo-but-run review)* · `episode-drafted-late` ×1 *(proposed — no episode draft existed at Construction Complete, drafted retroactively at Ship)* | **no baseline** — nothing was flagged at planning because no planning-quality gate ran for this feature specifically. All three surfaced in Build/Ship. `design-not-implementable` and `stale-context-propagated` did **not** recur | v2 | 2026-08-23 |
| F-013 | aspire-wiring | Skip | n/a | — | security-ac-unmaterialized:1, ac-uncovered:1, nfr-underspecified:2, dependency-missed:2, estimate-mis-scoped:1 | no-baseline (no Readiness Party row at plan) | v2 | 2026-08-18 |
| F-018 | api-refactor-foundations | Full | Fair (C:Fair T:Fair D:Fair) | ac-uncovered:1, task-orphan:1, dependency-missed:1 | *(pending Ship Reflect 16g)* | *(pending)* | v2 | 2026-08-18 |
| F-015 | api-gateway-and-mobile-contract | Full | Fair (C:Strong T:Strong D:Fair) | estimate-mis-scoped:1 *(Wave 3 plans T07 and T09 as parallel; both touch MobileApp's Infrastructure/Services layer with no formal dependency edge — adversarial re-check dropped Durability from Strong)* | `dependency-missed` ×2 *(T03's allowlist built against the context catalog, never re-checked against F-014's actual shipped routes — the messages/notifications gap; and no task depended on a PR existing early enough for `Mobile — iOS/Android Build`/`Integration` to run even once during Construction)* · `requirement-missing` ×1 *(AC13's wording assumed the report/payment screens already existed; no task built them until T11 caught it)* | misses: `dependency-missed`:2, `requirement-missing`:1; known: 0; caught: `estimate-mis-scoped`:1 *(flagged at plan; Wave 3's T07/T09/T12 merged with zero conflicts, so the flagged risk did not surface)* | v2 | 2026-08-24 |
| F-017 | container-and-cd-hardening | Full | Fair (C:Strong T:Fair D:Strong) | `security-ac-unmaterialized` ×1 *(Step 14.5 was skipped during initial task creation — T-001/T-002 existed only as prose in task descriptions; caught during this same readiness check, corrected before the row was written: PRD ACs 14-15 back-written, materialized via `tasks.cjs ac add` on F-017-T09/T05, confirmed via `tasks.cjs check`)* | *(pending Ship Reflect 16g)* | *(pending)* | v2 | 2026-08-25 |

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
