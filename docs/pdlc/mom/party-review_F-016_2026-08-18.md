# MOM — Party Review

**Feature:** `secure-public-endpoints` (F-016) · **Date:** 2026-08-18T23:15:00Z
**Called by:** Neo (Architect, Construction lead)
**Participants:** Neo, Echo, Phantom, Jarvis — 4 reviewers
**Spawn mode:** **solo** — standing session instruction not to spawn agents overrides STATE's
`Party Mode: agent-teams`. Same condition as every prior F-016 meeting.
**Review file:** [`docs/pdlc/reviews/REVIEW_secure-public-endpoints_2026-08-18.md`](../reviews/REVIEW_secure-public-endpoints_2026-08-18.md)
**Blast radius:** [`docs/pdlc/reviews/BLAST-RADIUS_secure-public-endpoints_2026-08-18.md`](../reviews/BLAST-RADIUS_secure-public-endpoints_2026-08-18.md)

---

## Who did not participate, and why

**Muse — not convened.** `ux-review.md` records **Triage outcome: Skip (0/3)** (no user-facing UI surface), so
the Step 12 conditional does not fire and no design-time scorecard exists to delta against.

**Consequence for Step 12.5:** the Variant Convergence trigger gate fails on precondition (a) before its three
regression signals are evaluated. Skip record appended to `ux-review.md`. **A post-review reader does not need
to re-read that file to predict this** — the gate cannot fire for F-016.

---

## Round 1 — what each reviewer flagged

| Reviewer | Critical | Important | Advisory | Verdict |
|---|---|---|---|---|
| **Neo** — architecture, PRD conformance, YAGNI lens | 0 | 1 *(folded)* | 1 | PASS with one deviation |
| **Phantom** — security + threat-mitigation check | 0 | 2 | 3 | PASS |
| **Echo** — coverage | 0 | 1 | 2 | PASS with one gap |
| **Jarvis** — docs & contracts | 0 | 2 | 1 | PASS with two staleness items |

**Tally after cross-talk: 0 Critical · 5 Important · 7 Advisory.**

- **Neo** found no drift from `ARCHITECTURE.md`, spot-checked 6 of the 26 AC rows against code, and raised the
  projection asymmetry (providers projected, customers not). Over-engineering lens produced three one-liners,
  all judged genuine trades rather than dead weight.
- **Phantom** raised the unprojected providers-list cache (I-1) and the concrete customer payload still visible
  to a Provider-role caller (I-2). His threat-mitigation check **passed all seven** "mitigate now" threats
  with code *and* a linked asserting test — no "citation over code" gap.
- **Echo** found the one real coverage gap: **AC-14 is verified on 1 of 6** remaining local catch sites, with
  Booking's three having no integration coverage at all.
- **Jarvis** found `CLAUDE.md` stale in the two places agents read first (test counts, and the integration
  command absent entirely), and that the catalog line which propagated the 10-vs-9 handler error is still
  unfixed pending the Ship refresh.

---

## Round 2 — cross-talk

**Interconnection 1 — Neo N-1 ↔ Phantom I-1 ↔ Phantom I-2. Consensus in round 1.**
All three are the same underlying decision: the projection is applied at the response boundary of one route
family only. Primary finding: **I-2** (concrete, quantified). **I-1** filed separately because its fix differs
in kind — containment/ordering vs a scope decision already recorded in ADR-026. **N-1** folded in as the
architectural framing rather than a third finding.

**Interconnection 2 — Phantom I-1 ↔ Echo A-5. Judged independent.**
Both concern "the cache", but I-1 is *what the cache holds* relative to what the endpoint returns, and A-5 is
that `CacheAside` has no test and fails open to `default!`. Neither fix resolves the other. Filed separately,
with a note that A-5 is why an I-1 regression would be hard to observe.

**Critical routing to Neo:** not triggered — Phantom and Echo raised no Critical findings.

Cross-talk closed after **1 round**; no disagreement survived, so rounds 2–3 were not used and the deadlock
protocol did not apply.

---

## Step 12.6 — Nordstrom standards assessment: COULD NOT RUN

`enforcing` tier, full-codebase band. Probed before announcing a 5–12 minute estimate: the plugin is
installed, but its sources do not resolve (`nordstrom-engineering-standards`,
`nordstrom-security-standards` — no response), there is no local `.nordstrom-standards/` cache, and no prior
`docs/standards-readiness/` report to `--delta` against.

Treated as **skip-with-notice (plugin unavailable)** per the gate's own fallback, **not** as a user
`/override` — so no ADR is minted and no MUST/P1 blockers enter Step 13.

⚠️ **Fourth consecutive gate blocked by this** (F-013 ship, F-018 Define, F-016 Define, F-016 Plan, F-016
Review). Logged in STATE's Guardrail Log with the recommendation that it be folded into F-017 — a gate marked
`enforcing` that has never executed is not enforcing anything.

---

## Step 13a — Phantom security sign-off

**✓ No Critical or Important *security regressions*.**

Phantom's two Important findings (I-1, I-2) are **newly-visible adjacent exposure**, not regressions: I-2 is
the exposure class ADR-026 explicitly deferred, now quantified against the actual payload; I-1 is a
containment trap that is correct today. Both are safe to accept at this gate and both are recorded.

All seven threat mitigations verified in code with linked tests; `tasks.cjs check` → **0**
`security-ac-untested`.

Per Step 13a's wording, because Phantom flagged Important-but-not-Critical findings, they are presented as
soft warnings with this summary: **Phantom recommends fixing I-1 before ship** (it is cheap — either a
regression test mirroring Calendar's, or projecting before caching) and **accepting I-2** as already-decided
by ADR-026.

---

## Blockers at Step 13

**None.**

- Critical findings: **0**
- Unresolved MUST/P1 standards gaps: **0** (assessment could not run — recorded, not silently absent)
- `tasks.cjs check` `security-ac-untested`: **0**

One item requires a human **decision** rather than a fix: the **AC-19 deviation** (one pre-existing test
deleted because ADR-025 removed its subject). All four reviewers concur the trade is sound; only the
maintainer can accept a deviation from an approved acceptance criterion.
