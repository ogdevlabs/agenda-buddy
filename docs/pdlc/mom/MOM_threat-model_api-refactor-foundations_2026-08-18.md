# MOM — Threat Modeling Party: api-refactor-foundations (F-018)

**Date:** 2026-08-18 · **Lead:** Phantom (Security Reviewer)
**Participants:** Phantom (lead), Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday
**Meeting mode:** run inline by the lead — subagents not spawned (not requested this session)
**Deliverable:** [`threat-model.md`](../design/api-refactor-foundations/threat-model.md)

---

## Triage

| Gate | Answer | Evidence |
|---|---|---|
| Trust boundary changes | **yes** | A token-issuing capability (RSA keypair + factory minting valid RS256 tokens for arbitrary subjects); `InternalsVisibleTo` permanently widens 7 production assemblies |
| Regulated data | **yes** | Email is the PII of record (CONSTITUTION §4). The harness resolves connection strings from configuration, and a misresolution reaches a live cluster holding real client PII |
| New attack surface | **yes** | Committed OpenAPI specs are a new published information artifact **on a public repo**; a new CI job pulls images from an external registry |

**3/3 → Full.** Party convened.

---

## The fact that reframed the meeting

Phantom opened by verifying repository visibility rather than assuming it, because several candidate threats' severity depended on it:

```
unauthenticated GET https://api.github.com/repos/ogdevlabs/agenda-buddy → HTTP 200
```

**The repository is PUBLIC.** Phantom then verified whether the known Atlas credential is reachable from *published* history rather than only local history:

```
9 commits reachable from origin/main contain "mongodb+srv://agenda_buddy"
earliest: ddb23ba
password literal still extractable from Calendar/appsettings.Development.json at that commit
```

This changed two severities and dominated the rest of the meeting:
- **T-001 → CRITICAL.** Every prior record (`ISSUE-002`, `STATE.md`, episode 001) describes this as *"in git history and still valid"*, which reads as an internal risk. It is a **live database credential published on the public internet**, in a repo class that credential scanners continuously harvest.
- **T-004 → HIGH** (from MEDIUM). The consequence of the harness accidentally reaching production is far worse when the production credential is publicly available and the cluster has no backups.

**Atlas (business impact):** the cluster holds client names, emails, phone numbers and appointment records — who met which therapist or coach and when, sensitive by inference. Notifiable breach, 72-hour GDPR clock, **no backups** to restore from.

---

## Layer 1 — Surface threats (divergent)

| Threat | Boundary | STRIDE | Raised by |
|---|---|---|---|
| T-001 credential publicly recoverable | TB-5 | I → E | Phantom |
| T-002 token factory is an auth-bypass primitive | TB-4 | S / E | Phantom |
| T-003 committed specs index anonymous endpoints | TB-5 | I | Phantom + Atlas |
| T-004 harness could target the live cluster | TB-2 | T / I | Bolt |
| T-005 container image tags are mutable | TB-6 | T | Pulse |
| T-006 `InternalsVisibleTo` widens 7 assemblies | TB-7 | E | Neo |
| T-007 spec-drift control is process-only | TB-5 | R | Echo |
| T-008 synthetic PII in logs | TB-3 | I | Muse |
| T-009 orphan containers exhaust the VM | TB-1 | D | Pulse |
| T-010 Testcontainers transitive CVEs | TB-6 | T | Phantom |
| T-011 reaper container holds Docker socket access | TB-1 | E | Pulse |

### Cross-talk highlights (the value of the round)

**Chain 1 — T-002, found by Phantom → Pulse → Neo.**
> **Phantom:** "To test 401 and 403 we need to mint tokens the services *accept*. That means holding a private key and forging any subject. We're building an auth-bypass tool and checking it into a public repo."
> **Pulse:** "F-013 already hit the analogous problem in CI and solved it — the startup guard generates a throwaway keypair in-step instead of storing `secrets.CI_JWT_*`. Same pattern applies."
> **Neo:** "Agreed, and add the reference direction. If any production csproj ever references the test project, the factory ships. 'Nobody would do that' is not a control — we already have precedent for asserting it, the AppHost-must-not-reference-MobileApp guard."

**Chain 2 — T-004, found by Bolt → Neo.**
> **Bolt:** "`MongoConnectionResolver` reads `ConnectionStrings:mongodb` *first*. DEPLOYMENTS.md says developers export that by hand for standalone runs. If the harness doesn't override unconditionally, tests run against whatever is exported."
> **Neo:** "And design D1 makes that worse, not better. Isolation now works by creating a unique database *per test*. Pointed at Atlas, the suite litters production with junk databases and synthetic client records — against a cluster with no backups. Fail closed: assert the endpoint is the container's before any test runs."

**Chain 3 — T-003, unresolved. Phantom → Neo → Jarvis.**
> **Phantom:** "The spec documents which endpoints are anonymous. F-016 says `GET /api/v1/providers` is anonymous, unpaginated, and returns customer emails. We're publishing a map to a live PII leak on a public repo, before F-016 fixes it."
> **Neo:** "Counter: the source is already public. Anyone reading `Provider/Program.cs` learns the same thing. The spec lowers effort, it doesn't create exposure."
> **Jarvis:** "And the reason we adopted the spec — which the human chose over Neo's own objection — is that contract drift must be visible in review. F-015's mobile mismatch survived the project's whole life for want of exactly this artifact. Withholding it during F-019/F-020 removes it precisely when contracts are changing."

**Phantom's ruling:** no consensus, and the disagreement is a values trade rather than a technical one. **Escalated to the human as Q1 rather than voted on** — the human owns acceptance at Step 12 by design.

---

## Layer 2 — Prioritisation (DREAD-flavoured)

| Threat | Damage | Reprod. | Exploit. | Affected | Discover. | Severity |
|---|---|---|---|---|---|---|
| T-001 | Critical — full R/W to client PII, no backups | Trivial | Trivial — `git log -S` | All clients | **Trivial — public repo, actively scanned** | **CRITICAL** |
| T-002 | High — forge any identity | Trivial | Requires the key to leak | All users | Low today | **HIGH** |
| T-003 | High — indexes a live PII leak | Trivial | Trivial | All providers + their customers | Trivial once committed | **HIGH** |
| T-004 | High — writes to production, no backups | Easy | Accidental, not adversarial | All clients | n/a | **HIGH** |
| T-005 | Medium — RCE on dev/CI | Hard | Requires upstream compromise | Developers | Low | **MEDIUM** |
| T-006 | Low–Medium | n/a | Needs repo write access already | n/a | Low | **MEDIUM** |
| T-007 | Medium — silent contract change | Easy | Requires only inattention | API consumers | Low | **MEDIUM** |

T-008 through T-011 dropped from active discussion as LOW, recorded in the threat model.

**Echo on reproducibility:** T-001, T-002, T-003 and T-004 are all trivially testable, so every "mitigate now" here can carry a real failing-first test. No mitigation in this model needs to rest on attestation.

---

## Layer 3 — Proposals

| Threat | Bucket | Proposer | Dissent |
|---|---|---|---|
| T-001 | **Mitigate now**, split: human rotation (outside F-018) + F-018 fail-closed guard | Phantom | none |
| T-002 | **Mitigate now** — in-memory per-session keypair; CI asserts no production reference | Phantom, Pulse, Neo | none |
| T-003 | **Open question Q1** | escalated | Neo + Jarvis vs Phantom — recorded, unresolved |
| T-004 | **Mitigate now** — ignore ambient config; assert container endpoint before any test | Bolt, Neo | none |
| T-005 | **Mitigate later** (ADR) — digest pinning | Pulse | Friday: update burden outweighs marginal risk for a first-party image |
| T-006 | **Accept** (ADR) | Neo | none — alternatives (public `Program`, strong-naming 7 assemblies) are worse trades |
| T-007 | **Mitigate later** (ADR) — CODEOWNERS or PR label, owned by F-019/F-020 | Echo | none |

**Bolt on feasibility:** all three "mitigate now" mitigations are small — an endpoint assertion, an in-memory keygen, and a CI reference check. None affects production code, which keeps the PRD's "no behaviour change" NFR intact.

**Muse:** no UX impact from any mitigation. Nothing user-facing exists here.

---

## Open questions for the human

**Q1 — Should the OpenAPI specs be committed now, given the repo is public and F-016's unauthenticated PII endpoint is unfixed?** Three options recorded in the threat model, including a middle path (generate and drift-check in CI as an artifact, commit only after F-016 ships).

**Q2 — Is the credential rotation being treated with the urgency public exposure implies?** Not an F-018 question. Phantom raised it anyway and declines to sign off a security review without it being asked. Given a public repo, real client PII and no backups, this is plausibly more urgent than the entire refactor programme.

---

## Meeting note

Phantom's substantive contribution this round was **checking a premise instead of inheriting it**. Every existing project record described the credential exposure in terms that understated it; a single unauthenticated HTTP request changed the severity of the most important finding in the model. That is the same lesson episode 001 recorded about threat T-004 — a security claim asserted by citation rather than verified was simply wrong.
