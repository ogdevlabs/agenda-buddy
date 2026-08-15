# Metrics
<!-- pdlc-template-version: 2.4.0 -->
<!-- Append-only. Jarvis adds a row after every shipped feature.
     Used by Atlas for roadmap planning and by Doctor for trend analysis.
     Do not delete rows — they form the project's delivery history. -->

**Project:** Agenda Buddy
**Last updated:** 2026-07-30

---

## Delivery Metrics

| Episode | Feature | Type | Cycle Days | Test Pass % | Review Rounds | Strikes | Tier1 Overrides | Security Findings | Tasks | Date Shipped |
|---------|---------|------|-----------|-------------|---------------|---------|-----------------|-------------------|-------|-------------|
<!-- No episodes yet. -->

---

## Trend Summary

<!-- Updated by Jarvis after every ship. Shows how the latest episode
     compares to the project average and the previous episode. -->

No trends yet — ship your first feature to start tracking.

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
