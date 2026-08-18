# Threat Model — api-refactor-foundations (F-018)
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Full
**Convened:** 2026-08-18
**Lead:** Phantom (Security Reviewer)
**Participants:** Phantom (lead), Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday — run inline by the lead (subagents not spawned; not requested this session)
**Status:** **Approved with decisions** (Step 12, 2026-08-18)

---

## Triage Record

- **Trust boundary changes:** **yes** — the harness introduces a **token-issuing capability** (an RSA keypair plus a factory minting valid RS256 tokens for arbitrary subjects), and `InternalsVisibleTo` permanently widens the assembly-visibility boundary of all seven production services.
- **Regulated data:** **yes at triage time, corrected after.** The triage answered yes on the basis that a misresolved connection string reaches a live cluster holding real client PII. **The cluster in fact holds only synthetic/development data** (confirmed at the Step 12 gate), so no regulated data is in play. The triage tier is left at Full because the answer was reasonable on the evidence available, and because the other two gates are yes regardless — but a re-triage today would score 2/3, still Full.
- **New attack surface:** **yes** — committed OpenAPI specifications become a new published information artifact **on a public repository**, and a new CI job pulls container images from an external registry.

**Triage tier: 3/3 → Full.**

> **Two facts shaped this threat model, and the second corrected the first.**
> 1. `github.com/ogdevlabs/agenda-buddy` is a **PUBLIC** repository (verified by unauthenticated `GET /repos` → HTTP 200), and the Atlas credential is recoverable from **published** history.
> 2. The `agenda_buddy` cluster holds **only synthetic/development data** (confirmed by the maintainer at the Step 12 gate), contradicting several long-standing project records.
>
> Fact 1 alone pushed T-001 to CRITICAL. Fact 2 brought it back to MEDIUM. Both are recorded rather than the conclusion being quietly rewritten, because the sequence is the useful part.

---

## Trust Boundaries

| ID | Boundary | Crossing |
|---|---|---|
| TB-1 | Test harness → Docker daemon | Container create/destroy over `~/.rd/docker.sock` |
| TB-2 | Test harness → MongoDB container | Test data, and **the connection string that decides which database is reached** |
| TB-3 | Test harness → service host | Real HTTP requests through the real auth pipeline |
| TB-4 | Token factory → service authentication | **Credential issuance.** Mints tokens the services will accept |
| TB-5 | Repository → public internet | Committed artifacts: OpenAPI specs, git history, any key material |
| TB-6 | CI runner → container registry | Image pulls over the network |
| TB-7 | Test assembly → production assemblies | `InternalsVisibleTo` compile-time visibility |

---

## Threats Identified

### T-001 — Live Atlas credential is publicly recoverable from published history
**Boundary:** TB-5 · **STRIDE:** Information disclosure → Elevation of privilege · **Severity: ~~CRITICAL~~ → MEDIUM**

> **Re-graded at the Step 12 gate, 2026-08-18.** The maintainer confirmed the `agenda_buddy` cluster holds **only synthetic / development data — never records for real people**. The analysis below was written assuming real client PII, which was itself inherited from earlier project records that inferred the cluster's contents from its *schema* rather than verifying them. **There is no personal-data breach, no sensitive-data exposure, no notification duty and no GDPR clock.** All such statements below are struck. What survives, and still requires rotation: the credential is **valid**, publicly recoverable, grants **write** access to a **live** cluster with **no backups**, permits Atlas resource abuse billed to the project owner, and is a real credential into a live Atlas project. Corrected across `ISSUE-002`, `STATE.md`, `OVERVIEW.md`, `DEPLOYMENTS.md` and episode 001.
**Status: INHERITED — not introduced by F-018.** Surfaced here because this is the first security review since the repo's public status was verified.

**Finding.** The `agenda_buddy` MongoDB Atlas connection string, including its password, is recoverable from **published** history on a **public** repository. Verified: 9 commits reachable from `origin/main` contain it; the earliest is `ddb23ba`; the literal is extractable from `Calendar/appsettings.Development.json` at that commit. F-013 removed it from the working tree, which is **not** remediation.

**What this review actually added, stated precisely.** `ISSUE-002` **already** assumed public exposure — it says credentials in public repositories are "typically probed within minutes". So the public-repo framing was not new, and an earlier claim in this review that it "reframes the issue entirely" overstated the novelty. What this review genuinely added was **verification** (9 commits from `origin/main`, earliest `ddb23ba`, literal still extractable) and, via Q2, the **correction that the data is synthetic**. Assume-breach remains the right posture for the credential itself; the consequence of that breach is far smaller than every prior record claimed.

**Impact (Atlas + Muse), as corrected.** ~~Full read/write to a cluster holding client names, email addresses, phone numbers and appointment records… notifiable personal-data breach with a 72-hour GDPR clock.~~ **Struck.** Actual impact: full read/write to a **live development** cluster with **no backups** — an attacker can destroy or silently corrupt the dev dataset, and abuse Atlas storage/compute/egress at the owner's expense. Recovery would mean re-seeding, not breach notification.

**Bucket: MITIGATE NOW** — split across two owners, because F-018 can only own part of it:
- **Human-only, outside F-018:** rotate the credential at Atlas and review the cluster access log. The exposure window is the full public lifetime of commit `ddb23ba`, not since F-013. **Human decision at the gate: re-grade `ISSUE-002` now, complete Inception, then rotate** — rotation is human-only work that cannot be done from here in any case.
- **F-018 owns:** the harness must make it **impossible** for an integration test to reach a non-container database. See T-004's mitigation, which is the F-018-scoped half of this.

**Proposed `[security]` AC (T-001):** *Given any resolved MongoDB connection string that does not target a Testcontainer-managed endpoint, the harness refuses to run and fails with a message naming the offending host.* 🧪 test-first

---

### T-002 — The token factory is an authentication-bypass primitive living in the repo
**Boundary:** TB-4 · **STRIDE:** Spoofing / Elevation of privilege · **Severity: HIGH**

**Finding (Phantom).** To test 401 and 403 paths the harness must mint tokens the services *accept* — meaning it holds an RSA **private** key and can forge a valid identity for **any** subject. That is, by construction, an auth-bypass tool. Two ways it turns into a real vulnerability:
1. **Key persistence.** A committed keypair on a public repo is immediately harvestable. If it were ever the *same* key as a real environment, every service's auth is forged at will.
2. **Reference leakage.** If any production `.csproj` ever referenced `AgendaBuddy.IntegrationTests`, the factory ships.

**Cross-talk (Phantom → Pulse → Neo).** Pulse noted F-013 already solved the analogous CI problem by generating a throwaway keypair in-step rather than storing `secrets.CI_JWT_*`. Neo confirmed the same pattern applies, and added the reference direction is worth asserting mechanically — "nobody would do that" is not a control.

**Bucket: MITIGATE NOW.**
- Keypair generated **in memory, per test session, never written to disk** (ARCHITECTURE D2).
- The key is generated fresh per run, so it is never the same key as any real environment.
- CI asserts **no production project references the integration test project** — the same shape as the existing "AppHost must not reference MobileApp" guard, which is precedent that this class of assertion works here.

**Proposed `[security]` AC (T-002):** *No PEM or private-key material appears in any tracked file, and no production `.csproj` references `AgendaBuddy.IntegrationTests` — both asserted in CI.* 🧪 test-first

---

### T-003 — Committed OpenAPI specs publish a map to a known unauthenticated full-record endpoint
**Boundary:** TB-5 · **STRIDE:** Information disclosure · **Severity: ~~HIGH~~ → MEDIUM** *(the `providers` endpoint returns synthetic data, so the original "map to a live PII leak" framing overstated it; it remains an unauthenticated full-record dump for F-016 to fix)*

**Finding (Phantom + Atlas).** F-018 commits an OpenAPI spec per service **to a public repository**. The spec documents every route, verb, and **which endpoints require authorization**. F-016 has already established that `GET /api/v1/providers` is **anonymous, unpaginated, and returns every provider's full record including embedded appointments carrying customer email addresses**.

Publishing the spec therefore hands an unauthenticated attacker a precise index of the anonymous endpoints — including the one that leaks customer PII — **before F-016 fixes it**.

**Honest counter-argument (Neo + Jarvis, recorded rather than suppressed).** The endpoints are already discoverable: the source is public, so anyone reading `Provider/Program.cs` learns the same thing. The spec lowers effort; it does not create the exposure. And the whole reason the spec was adopted (Jarvis's argument, which the human accepted over Neo's) is that contract drift must be visible in review — the F-015 mobile mismatch went unseen for the project's life for want of exactly this artifact.

**RESOLVED at the Step 12 gate — the middle path.** Specs are **generated and drift-checked in CI from day one, but not committed** until F-016 closes the anonymous PII endpoint. This keeps the mechanical drift protection through the period F-019/F-020 change contracts, while not publishing a precise index of anonymous endpoints on a public repo while one of them leaks customer emails.

Two consequences: **AC-17 changes** from "a spec is committed for each service" to "a spec is generated and drift-checked in CI for each service"; and **committing the specs becomes an F-016 exit criterion**, so the deferral cannot be forgotten. Note the residual severity is lower than first assessed anyway — the `providers` endpoint returns synthetic data, so the "map to a live PII leak" framing overstated it. The endpoint is still an unauthenticated full-record dump that F-016 must fix.

---

### T-004 — The harness could target the live production cluster
**Boundary:** TB-2 · **STRIDE:** Tampering / Information disclosure · **Severity: MEDIUM** *(briefly raised to HIGH on the assumption of real production PII; returned to MEDIUM once the cluster was confirmed synthetic. The mitigation is unchanged — writing junk into a live no-backup cluster is still unacceptable.)*

**Finding (Bolt).** `MongoConnectionResolver` reads `ConnectionStrings:mongodb` **first**, then environment, then `appsettings`. A developer running the AppHost locally may well have a real connection string exported, and the DEPLOYMENTS.md gotchas confirm developers set `ConnectionStrings__mongodb` by hand for standalone runs. If the harness does not *explicitly and unconditionally* override it, integration tests execute against whatever the environment supplies.

**Consequence, made worse by design D1.** Isolation now works by creating a **unique database per test**. Pointed at the real cluster, the suite would (a) write synthetic client records into production, (b) create a litter of junk databases, and (c) write junk audit events — against a cluster with **no backups**.

**Bucket: MITIGATE NOW.** Fail closed rather than trusting configuration precedence:
- The harness sets the connection string from the container and **ignores ambient environment values**.
- Before any test executes, it **asserts** the resolved endpoint is the Testcontainer's mapped host/port; anything else aborts the run.

**Proposed `[security]` AC (T-004):** *With `ConnectionStrings__mongodb` exported to a non-container value, the harness aborts before executing any test and names the rejected host.* 🧪 test-first

---

### T-005 — Container image tags are mutable
**Boundary:** TB-6 · **STRIDE:** Tampering · **Severity: MEDIUM**

**Finding (Pulse).** AC-11/12 pin images by **tag** (`mongo:7.0.14`), which is reproducible in intent but not in fact — a tag can be repointed at a different image. A compromised or repointed upstream tag executes attacker-controlled code on developer machines and CI runners.

**Bucket: MITIGATE LATER** — pin by **digest** (`mongo@sha256:…`) rather than tag. Deferred because digest pinning adds an update burden (every upgrade edits a hash) and the marginal risk over a pinned patch tag from a first-party image is modest. **Requires an ADR** per the deferral rule. Revisit if the harness ever pulls a non-first-party image.

---

### T-006 — `InternalsVisibleTo` permanently widens seven production assemblies
**Boundary:** TB-7 · **STRIDE:** Elevation of privilege (design-level) · **Severity: MEDIUM**

**Finding (Neo).** Each service gains `<InternalsVisibleTo Include="AgendaBuddy.IntegrationTests" />` **permanently**. Since the assemblies are not strong-named, *any* assembly built with that name can access their internals — so the grant is to a name, not an identity.

**Assessment (Phantom).** Low practical exploitability: an attacker able to add a project to the build already has repository write access, which is a larger problem. But it is a real, permanent widening, and it will be replicated across seven assemblies.

**Bucket: ACCEPT**, with rationale recorded: `WebApplicationFactory` requires entry-point visibility; the alternatives (making `Program` public, or strong-naming seven assemblies) are worse trades for a test-only need. **Requires an ADR** per the acceptance rule.

---

### T-007 — The spec-drift control is process-only where it matters most
**Boundary:** TB-5 · **STRIDE:** Repudiation · **Severity: MEDIUM**

**Finding (Echo).** AC-19 makes CI fail on an un-regenerated spec — a real mechanical control. But the *purpose* of the spec (per the decision that adopted it) is that contract changes are **reviewed**. Regenerating and committing satisfies CI while nobody reads the diff, and the stated obligation — "an unreviewed spec diff is a defect" — has no enforcement whatsoever.

**Bucket: MITIGATE LATER.** F-018 delivers the mechanical half. Making the review half real (e.g. a CODEOWNERS entry on the spec path, or a PR label) belongs with F-019/F-020, which are the features that will actually change the contract. **Requires an ADR.**

---

## Threats Noted but Not Prioritized

| ID | Threat | Severity | Why not prioritized |
|---|---|---|---|
| T-008 | Synthetic PII (`customer.pii@example.com`) in test logs | LOW | Synthetic data only; `PiiRedactingProcessor` exists, and telemetry export is inert in test hosts (`OTEL_EXPORTER_OTLP_ENDPOINT` unset) |
| T-009 | Orphan containers exhaust the 2 CPU / 4.1 GB VM | LOW *(security)* | Real, but a local availability nuisance rather than a security threat. Already covered by AC-13 |
| T-010 | `Testcontainers.MongoDb` transitive CVEs (SharpCompress, Snappier) | LOW | **Already mitigated** — `Directory.Build.props` pins Snappier 1.3.1 and SharpCompress 0.50.1 solution-wide. An initial reading of spike warnings as a new finding was a false alarm from a throwaway project lacking those pins |
| T-011 | Testcontainers' reaper container itself has Docker socket access | LOW | Inherent to Testcontainers; accepted as a property of the chosen tool |

---

## Open Questions for Human

**Q1 — T-003: should the OpenAPI specs be committed now, given the repo is public and F-016's unauthenticated PII endpoint is still unfixed?**
The party could not resolve this; it is a values trade.
- **Commit now:** the contract becomes reviewable immediately, which is why it was adopted. The endpoints are already discoverable from public source, so the spec lowers attacker effort rather than creating exposure.
- **Defer spec publication until F-016 lands:** avoids publishing a precise index of anonymous endpoints while one of them leaks customer emails. Costs the review benefit for exactly the period F-019 is changing contracts — the period it is most needed.
- **Middle path:** generate and drift-check the specs in CI but **do not commit them** until F-016 ships (artifact-only), then commit.

**Q2 — T-001: is the credential rotation being treated with the urgency public exposure implies?**
Not an F-018 question, but Phantom will not sign off a security review without asking. Every existing record understates this as a history/internal issue. Given a public repo, no backups, and real client PII, this is arguably more urgent than the entire API refactor programme. Should `ISSUE-002` be re-graded and actioned before F-018 proceeds?

---

## Approval Outcomes (filled in at Step 12)

| Threat | Severity | Party proposal | Human decision | Notes |
|---|---|---|---|---|
| T-001 | ~~CRITICAL~~ **MEDIUM** | Mitigate now (split) | **Accepted, re-graded.** Cluster confirmed synthetic → no personal-data breach. Re-grade `ISSUE-002` now, finish Inception, then rotate | F-018 owns the fail-closed half (see T-004). Rotation is human-only |
| T-002 | HIGH | Mitigate now | **Accepted** | `[security]` AC at Plan 14.5 |
| T-003 | ~~HIGH~~ **MEDIUM** | Open question Q1 | **Resolved — middle path.** Generate + drift-check in CI now; **commit only after F-016** | AC-17 reworded; committing becomes an F-016 exit criterion |
| T-004 | ~~HIGH~~ **MEDIUM** | Mitigate now | **Accepted** | `[security]` AC at Plan 14.5. Mitigation unchanged by the re-grade |
| T-005 | MEDIUM | Mitigate later (ADR) | **Accepted** | ADR-018 |
| T-006 | MEDIUM | Accept (ADR) | **Accepted** | ADR-019 |
| T-007 | MEDIUM | Mitigate later (ADR) | **Accepted** | ADR-020, owned by F-019/F-020 |

---

## Revision History

| Date | Change | Author |
|---|---|---|
| 2026-08-18 | Initial threat model, Full triage (3/3). Repo verified **public**, which raised T-001 to CRITICAL and T-004 from MEDIUM to HIGH. | Phantom |
| 2026-08-18 | **Step 12 gate.** Maintainer confirmed the cluster holds only synthetic/development data. T-001 CRITICAL→MEDIUM, T-004 HIGH→MEDIUM, T-003 HIGH→MEDIUM and resolved via the middle path. The overstated PII/GDPR claims were corrected across five other project documents. **The lesson: a schema tells you what data could be there, not what is — and this project asserted the alarming reading for three weeks without anyone checking.** | Phantom |
