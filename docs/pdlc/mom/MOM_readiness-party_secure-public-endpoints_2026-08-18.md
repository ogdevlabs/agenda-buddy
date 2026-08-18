# MOM — PRD + Plan Readiness Party: secure-public-endpoints (F-016)

**Date:** 2026-08-18
**Lead:** Atlas (Product Manager), co-chairing with Neo (Architect)
**Participants:** Atlas, Echo, Neo, Phantom, Jarvis — 5 agents. **Muse not required** (Step 10.6 triaged Skip, so there are no fix-now UX findings to confirm landed as tasks).
**Spawn mode:** `solo` — standing no-Agent-tool instruction overriding STATE.md's `Party Mode: agent-teams`. Recorded rather than silently substituted.
**Scope discipline:** documents-only. The PRD, plan file, task records, `threat-model.md` and `ux-review.md`. **No product source was read for grounding** — that belongs at Build pre-flight Step 2b.
**Nature:** fully **advisory**. It did not block approval and has no `/override` path.

---

## Triage

| Input | Value |
|---|---|
| Task count | **19** |
| Waves | **7** |
| Domains | `backend`, `devops`, `security` |
| Unresolved MUST requirements | no |

**Tier: Full** (multi-wave AND multi-domain AND ≥6 tasks — all three Full conditions).

---

## Outcome: **Fair** — 4 open gaps, one of them a real defect corrected in-party

| Dimension | Rating |
|---|---|
| Completeness | Fair |
| Traceability | Fair |
| Durability | Fair |

Full evidence-backed scorecard lives in the PRD's `## Readiness Assessment` section. This MOM records the *process* — who found what, and how.

---

## 🔴 The finding that justified convening: AC-12 contradicted ADR-025

> **Echo:** "I don't build the matrix from the requirements. I build it from the ACs and cross-check against the **ADR registry**, because the ADRs are what actually changed during Design. AC-12 says *'`POST /api/v1/professions` returns 403 for a caller who does not hold the required role.'* ADR-025 **deletes that route.**"
>
> **Atlas:** "So Build would implement a role check on a route that isn't there —"
> **Echo:** "— and worse, the *correct* behaviour, 404 or 405, would read as a test failure. Someone would 'fix' it by restoring the route."
> **Neo:** "The requirement text was annotated correctly. Requirement 13 says superseded, and the API contract says deleted. The acceptance criterion is the one artifact that didn't get updated."
> **Atlas:** "Strike it, point it at AC-26, and record why. This is the drift class the party exists for: a Design-gate decision propagated into three documents and missed the fourth."

**Corrected in-party.** AC-12 struck, replaced by AC-26 (`[security]`, T-007). Recorded in `METRICS.md` as `ac-contradicts-adr` ×1.

**Process observation.** This was found by checking ACs against **ADRs**, not against requirements. A requirements-only traceability pass would have shown AC-12 as *covered* — requirement 13 exists, AC-12 tests it, task T17 implements it. Every link was present; the link was to a superseded decision. Worth keeping as a standing check: **when a design gate changes a decision, the AC list is the artifact most likely to be missed.**

---

## Phantom's issue-#55 check — ✅ passes, after a false alarm worth recording

> **Phantom:** "Seven mitigate-now threats. I need seven `[security]`-tagged ACs on tasks — not task-body citations. `ac list --json` on all seven returns `tag=None threat=None`. That reads as **`security-ac-unmaterialized`** across the board, which would be a blocking-severity finding."
>
> **Jarvis:** "Check the task files rather than the projection."
>
> **Phantom:** "`F-016-T06` carries `acceptance_criteria: [\"AC1|security|T-002|…\"]`. The tag and the threat id **are** persisted. And `tasks.cjs check` reports exactly seven `security-ac-untested` findings for F-016 — a check that only fires on `[security]`-tagged ACs. So the data is right and the `--json` projection just doesn't surface those fields under the key names I assumed. **Withdrawn.**"

Verified state: all 7 materialized (`T06`/T-002, `T08`/T-004, `T09`/T-001, `T13`/T-006, `T16`/T-003, `T17`/T-007, `T18`/T-005). No `security-ac-unmaterialized`, no `design-finding-unlinked`.

Kept in the record because **the tooling's read path disagreed with its own write path**, and the next person to run that command will draw the same wrong conclusion. Verify against the task files, not `ac list --json`.

---

## Neo's durability finding: the feature's thesis is unenforced

> **Neo:** "The dependency graph is sound — acyclic per `tasks.cjs check`, seven waves, critical path nine deep, two bottlenecks named. Decomposition granularity is right; nothing is a 'build the whole feature' task and nothing is a one-liner.
>
> My concern is elsewhere. This feature's entire argument is *'endpoint authorization you can demonstrate'* — that's why it absorbed six tasks from F-018 instead of shipping unit tests. But **F-018's T18, the integration CI job, was not absorbed.** So the demonstration runs on one developer's laptop and nothing enforces it on a push or a PR.
>
> That is the weakest link in the plan, and it is structural rather than accidental — we scoped the harness in and the enforcement out."
>
> **Atlas:** "Options?"
> **Neo:** "Pull T18 forward as an eighth absorbed task, or accept local-only and write it down. I won't pick — it's a scope call."
> **Echo:** "Worth noting the failure mode isn't 'tests don't run.' It's that they run *until someone stops running them*, and nothing notices."

Recorded as gap 1, escalated to the Step 18 prompt rather than decided by the party.

---

## Atlas's completeness findings

- All 12 PRD sections populated and specific; 9 explicit out-of-scope exclusions each with a reason and an owning feature.
- **Requirements 8 and 20 have no dedicated AC.** Both are meta-requirements — req 8 ("every authz change covered by an integration test") is satisfied structurally by AC-8…AC-18 all being integration-level; req 20 ("no success-path semantics change") is covered by AC-19's no-regression clause. Defensible; flagged as `requirement-without-dedicated-ac` ×2 so it does not later read as an omission.
- **Standards alignment could not be assessed at either gate.** The plugin is installed but its six source repos are unreachable. Atlas's note: *"the enforcing gate produced no MUST findings because it could not compute any — that is not the same as clean, and the PRD now says so explicitly."*

---

## Operational finding

**`tasks.cjs ready` is not feature-scoped.** It returns `F-018-T02`, `F-018-T04` and `F-018-T19` alongside `F-016-T01`, because F-018 is paused-and-unclaimed but its tasks remain open. The Build loop's Step 5 selects by label priority, not by feature, so it could start an F-018 task. **Filter on `epic:secure-public-endpoints`.** Recorded as `tooling-scope-leak` ×1.

---

## Readiness Trend

Row appended to `docs/pdlc/memory/METRICS.md`: `Fair` overall, 4 gaps at plan. The surfaced-later and delta columns are filled by Jarvis at Ship Reflect Step 16g — that reconciliation is the actual planning-quality signal, not this row on its own.

For context, the two prior Full-triage features also came back **Fair** (F-018: 3 gaps; F-013 was Skip-triage and had 7 gaps surface later). Three consecutive Fair ratings is not yet a recurrence signal under the 3-of-4 rule, but the *categories* differ each time, which is mildly reassuring — it suggests the party is finding real, feature-specific drift rather than the same systematic blind spot.
