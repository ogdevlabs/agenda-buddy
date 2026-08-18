# MOM — PRD + Plan Readiness Party: api-refactor-foundations (F-018)

**Date:** 2026-08-18 · **Lead:** Atlas (Product Manager), co-chairing with Neo (Architect)
**Participants:** Atlas (lead), Echo (traceability), Neo (durability), Phantom (threat→AC materialization; Step 10.5 ran), Jarvis (documentation)
**Muse:** not included — Step 10.6 triaged **Skip**, so there are no fix-now UX findings to confirm
**Meeting mode:** run inline by the lead — subagents not spawned (not requested this session)
**Status:** **Advisory.** Never blocks approval; the human decides at Step 18.

---

## Triage

| Input | Value |
|---|---|
| Task count | **20** |
| Waves | **7** |
| Domains (from task labels) | `backend`, `devops`, `docs`, `security` |
| Unresolved MUST requirements | no |

Multi-wave **and** multi-domain **and** ≥6 tasks → **Full**.

**Scope discipline observed:** this party reviewed only the PRD, the plan file, the task records, `threat-model.md` and `ux-review.md`. It did **not** read product source for grounding — that belongs at Build pre-flight. Every citation below is to a planning artifact.

---

## Sub-procedure 2 — Traceability matrix (Echo)

Built from the artifacts, not from memory of the conversation.

**31 acceptance criteria, 27 requirements, 20 tasks.**

| AC | Task(s) | | AC | Task(s) |
|---|---|---|---|---|
| 1, 2 | T05 | | 17, 18 | T16 |
| 3 | T06 | | 19 | T17 |
| 4, 12, 13b | T08 | | 20, 21 | T18 |
| 5 | T11 | | 22 | T19 |
| 6, 8 | T12 | | 23, 25 | T02 |
| 7, 15 | T13 | | 24 | T20 |
| 9 | T09 (mechanism) + T14 | | 26 | T03 |
| 10, 11, 14 | T07 | | 27 | T04 |
| 13 | T15 | | **28, 29** `[security]` | **T08** |
| 16 | T01 | | **30** `[security]` | **T06** |

**AC → task: closes completely.** All 31 ACs have at least one covering task. No `ac-uncovered` in this direction.

**Task → AC: one break.** **F-018-T10** (`KafkaClientFake`) maps to no acceptance criterion. Its description says "supports AC-5/6/7", which is an enabling relationship, not coverage. → **`task-orphan`**.

**Requirement → AC: one break.** **Requirement 4** — *"The harness MUST NOT start Kafka: `IKafkaClient` is substituted with a recording fake … asserting the topic-creation call"* — has **no acceptance criterion**. → **`ac-uncovered`**.

**Echo's note:** these two gaps are one defect seen from both ends. ADR-017 states "the fake **records** calls so the topic-creation wiring is still asserted — the convention stays guarded, only the broker is faked." Nothing verifies that. As written, the plan could ship a fake that silently swallows the call and every test would still pass, quietly removing the guard ADR-017 claims to preserve.

---

## Sub-procedure 3 — Dimension scoring

### Completeness — Atlas — **Fair**

*Evidence for Strong:* 27 requirements present and grouped by concern; scope exclusions explicit (§Out of Scope, 7 items each with a stated reason); NFRs specified (7 items, including the measured 4.45 s constraint rather than a guess); 8 known risks each with deferral reasoning; all 31 ACs binary and `🧪 test-first` tagged; the TDD section carries a feature-specific note on which ACs must not be written after the fact.

*Gap:* Requirement 4 has no AC — `ac-uncovered`.

### Traceability — Echo — **Fair**

*Evidence for Strong:* the AC→task direction closes completely; **all three "mitigate now" threats are materialized as `[security]`-tagged ACs on tasks**, confirmed by running `tasks.cjs ac list` on T06 and T08 — so **no `security-ac-unmaterialized`**, the exact escape route issue #55 exists to close. `tasks.cjs check` reports 3 `security-ac-untested` findings, which is the expected state until Build links tests.

*Gap:* T10 has no AC — `task-orphan`.

### Durability — Neo — **Fair**

*Evidence for Strong:* dependency graph acyclic (34 F-018 nodes/edges rendered; generation would fail on a cycle); 7 waves ordered sensibly; the two bottlenecks (`T05`, `T08`) named explicitly rather than discovered later; critical path stated (`T01 → T05 → T06 → T08 → T13 → T18 → T20`, 7 deep); `T16` correctly lifted off the critical path on spike evidence that spec generation needs no container; task granularity reasonable in both directions.

*Gap:* **`T19 → T20` crosses a human gate the graph cannot express** — `dependency-missed`. `T17`, `T18` and `T19` all require a **maintainer-pushed throwaway branch** to verify, because `main` is PR-protected and pushing is disallowed. The dependency model has no way to represent "waits on a human", so the critical path contains an unschedulable step that looks like ordinary work in the graph.

---

## Sub-procedure 4 — Adversarial pass: **RAN**

Each lens initially rated its own dimension **Strong**. A skeptic challenged each, defaulting to refuted-if-uncertain. **All three dropped to Fair.**

| Dimension | Challenge | Outcome |
|---|---|---|
| Completeness | "Name the AC that verifies Requirement 4." | **Refuted.** There isn't one. Strong → Fair |
| Traceability | "'Every AC has a task' is half the matrix. Does it close the other way?" | **Refuted.** T10 has no AC. Strong → Fair |
| Durability | "The graph is sound as a graph — but can it actually be executed start to finish without a human?" | **Refuted.** Three tasks are gated on a maintainer push no edge represents. Strong → Fair |

**Worth recording:** a self-certified scorecard here would have read **Strong / Strong / Strong** and been wrong on all three counts. The adversarial pass is the only reason this row is honest, and it cost one round.

No pitch+vote was needed — all three challenges converged immediately.

---

## Cross-talk highlights

**Echo → Atlas → Neo, on the Kafka gap:**
> **Echo:** "T10 maps to no AC. On its own that's a bookkeeping nit."
> **Atlas:** "It isn't, because Requirement 4 has no AC either. Same hole from the other side."
> **Neo:** "And ADR-017 *claims* the convention stays guarded because the fake records the call. If nothing asserts the recording, the ADR is asserting a guarantee the plan doesn't deliver. One AC fixes the requirement, the orphan, and the ADR's claim together."

**Phantom, unprompted:**
> "For the record — the F-013 failure mode was a security claim asserted by citation with no asserting test. The Kafka gap is structurally identical: an ADR sentence standing in for a test. It's low-severity because Kafka only creates topics, but it's the same shape, and it's worth naming as such rather than filing as a nit."

---

## Gap list (D7 taxonomy `v2`)

| Category | Gap | Cheap fix? |
|---|---|---|
| `ac-uncovered` | Requirement 4 (Kafka fake asserts the topic-creation call) has no AC | **Yes** — one AC |
| `task-orphan` | F-018-T10 traces to no AC | **Yes** — the same AC |
| `dependency-missed` | `T17`/`T18`/`T19` gated on a maintainer-pushed branch; `T19 → T20` crosses it | No — inherent to the no-push constraint. Already documented in the plan's *Known gaps*; needs acknowledgement, not a fix |

---

## Recommendation (advisory)

**Overall: Fair — 3 open gaps.**

Two of the three share a single cheap fix: **add an acceptance criterion asserting `KafkaClientFake` records the topic-creation call.** That closes Requirement 4's verification gap, gives `T10` a home, and makes ADR-017's "the convention stays guarded" claim true rather than aspirational.

The third is structural and already disclosed. It should be acknowledged at the gate, not fixed.

**The human decides at Step 18.** Nothing here blocks approval.

---

## Open questions for the human

None beyond the recommendation above. The gaps are specific, categorized, and one of them has an obvious remedy the human can accept or decline in a sentence.
