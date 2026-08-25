---
feature: container-and-cd-hardening
topic: threat-model
date: 2026-08-25
mode: solo
participants: Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday
---

# Meeting Minutes: Threat Modeling Party
## Feature: container-and-cd-hardening | 2026-08-25

**Mode:** Solo
**Participants:** Phantom (Security Reviewer, lead), Neo (Architect), Bolt (Backend), Echo (QA), Pulse (Deployment), Atlas (Product Manager), Muse (UX Designer), Jarvis (Tech Writer), Friday (Frontend)

---

## Context

Design documents (`ARCHITECTURE.md`, `data-model.md`, `api-contracts.md`) for F-017 `container-and-cd-hardening` are generated and committed. Phantom's triage came back 2/3 yes (new trust boundary via third-party Actions entering the CI pipeline; new attack surface in the supply-chain sense, even though no user-facing endpoint is added) — Full tier, so the party convened per `skills/brainstorm/steps/threat-model.md` Phase B.

---

## Discussion

### Round 1 — Surface threats (divergent)

**Phantom:** Walked all four trust boundaries (TB-1 through TB-4) against STRIDE. Six candidate threats surfaced: unpinned third-party Actions (Tampering, TB-1), secret-value leakage into CI logs (Information Disclosure, TB-2), malicious transitive NuGet packages during the new per-service publish step (Elevation of Privilege, TB-3), resource exhaustion from the 7-way matrix (Denial of Service, TB-3), Dependabot bypassing review discipline (Repudiation, TB-4), and a workflow-file change weakening the new gates themselves (Spoofing/Tampering, TB-3).

**Pulse (cross-talk on T-004):** "The 7-way matrix plus Trivy's uncached CVE-database download is a real turnaround-time cost, but there's no attacker benefit — this repository has no external contributors triggering CI at volume today. I'd keep this LOW, not MEDIUM."
**Phantom:** Agreed — severity set to LOW, mitigated by the `timeout-minutes: 10` bound already in the PRD.

**Bolt (cross-talk on T-003):** "This isn't new exposure — `build-and-test` already runs `dotnet restore`/`build` on every PR today, same risk class. We're adding 7 more invocations of an existing accepted risk, not a new one."
**Phantom:** Agreed — recommendation set to Accept rather than Mitigate now; fixing "NuGet packages can run code" is a solution-wide concern out of scope for this feature.

### Round 2 — Prioritize (convergent)

**Atlas:** Business impact ranking — T-002 (secret leaking into CI logs) is the highest-stakes threat by far, since it would reproduce the exact class of exposure (`ISSUE-002`) this feature exists to prevent, in a location (CI logs) that's harder to scrub than git history. T-001 (unpinned Actions) is real but standard CI hygiene, not specific to this project's history.

**Echo:** T-001 and T-002 are both cheaply testable — a structural assertion on the workflow YAML for T-001, and a log-content assertion on the canary test already planned for T-002. T-003 through T-006 are not independently testable without inventing scope this feature doesn't need.

### Round 3 — Propose mitigations (convergent → actionable)

**Phantom's final buckets:**
- **T-001 (unpinned Actions), T-002 (secret-value log leakage):** Mitigate now — both cheap, both directly load-bearing for this feature's own stated purpose (closing the class of gap that let a real credential leak).
- **T-003 (malicious NuGet package), T-004 (resource exhaustion), T-005 (Dependabot review bypass), T-006 (workflow tampering):** Accept — all four are either pre-existing risk classes this feature doesn't change, or already mitigated by an existing control (branch protection) unrelated to this feature.

No disagreement required escalation beyond the one open question below (external-contributor policy), which needs org-specific context (whether this repo is/becomes public) that the party cannot resolve on its own.

---

## Conclusion

Six threats identified across four trust boundaries; two (T-001, T-002) recommended Mitigate now with testable acceptance criteria ready to back-write into the PRD at Plan; four (T-003–T-006) recommended Accept, each with a specific rationale tying back to either pre-existing risk or an existing control. No cross-agent disagreement required human arbitration beyond the one open question about external-contributor policy.

---

## Next Steps

| # | Action | Owner | Notes |
|---|--------|-------|-------|
| 1 | Human reviews and decides on all six threats at Step 12 | Human | Approval Outcomes table in `threat-model.md` |
| 2 | Human answers the external-contributor-policy open question | Human | Affects whether T-003's Accept rationale still holds |
| 3 | Back-write T-001, T-002 as `[security]`-tagged ACs at Plan (Step 13) if confirmed | Neo | Already drafted in `threat-model.md`'s "Tasks + security acceptance criteria" table |

---

## Escalation

**External-contributor policy:** if this repository is or becomes public on GitHub, does the maintainer want an explicit policy for fork pull requests before this feature ships, beyond the existing safe `pull_request` trigger already in use? Affects whether T-003's "no active external contribution" assumption holds.
