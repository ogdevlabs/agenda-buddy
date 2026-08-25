# Threat Model — container-and-cd-hardening (F-017)
<!-- pdlc-template-version: 1.0.0 -->

**Triage:** Full
**Convened:** 2026-08-25
**Lead:** Phantom (Security Reviewer)
**Participants:** Phantom, Neo, Bolt, Echo, Pulse, Atlas, Muse, Jarvis, Friday (solo mode — single LLM roleplaying all agents, per Party Mode's documented fallback; consistent with this feature's Progressive Thinking meeting)
**Status:** Pending human approval (Step 12)

---

## Triage Record

| Question | Answer | Evidence |
|---|---|---|
| Does this feature introduce or modify a trust boundary? | yes | Two new third-party GitHub Actions (gitleaks, Trivy) enter the CI pipeline's trust boundary for the first time (`ARCHITECTURE.md` "New CI jobs") |
| Does this feature touch regulated data (PII, payment, health, biometric, children's)? | no | This feature touches no application data at all — build/CI configuration only |
| Does this feature add a new attack surface (endpoint, event consumer, file upload, query interface, LLM tool, mobile handler)? | yes | Third-party Actions running with CI job context is a supply-chain attack surface, even though no new user-facing HTTP surface is added |

**Triage outcome:** Full (2/3 yes)

---

## Trust Boundaries

| ID | Boundary | What crosses | Trust direction | Diagram reference |
|---|---|---|---|---|
| TB-1 | GitHub Actions workflow → third-party Actions (`gitleaks-action`, `trivy-action`) | CI job context, ambient `GITHUB_TOKEN`, repository contents | trusted → semi-trusted (vendor-controlled code running with CI privileges) | `ARCHITECTURE.md` "New CI jobs" |
| TB-2 | Scan step output → CI job logs (public in this repo's Actions UI) | Finding metadata — file paths, line numbers, and (if misconfigured) matched secret values | trusted → public-visible | `ARCHITECTURE.md` "security-scan (new job)" |
| TB-3 | Pull request → CI execution | Source diff, triggering a `dotnet publish`/container build across all 7 services | semi-trusted (this repo has no external contributors today, but `pull_request` — not `pull_request_target` — is already the safe trigger type in use) → CI compute | `08-cicd-deploy.md` "Triggers" |
| TB-4 | Dependabot service → repository pull requests | Proposed dependency version bumps | external service → semi-trusted (still requires human PR review; no auto-merge in scope) | `ARCHITECTURE.md` ".github/dependabot.yml (new, standalone)" |

---

## Threats Identified

### T-001 — Unpinned third-party Actions allow a supply-chain substitution attack

- **STRIDE category:** Tampering
- **Trust boundary:** TB-1
- **Asset affected:** CI job's ambient `GITHUB_TOKEN` and repository contents during the run
- **Attack vector:** If `gitleaks-action` or the Trivy action is referenced by a mutable tag (`@v2`, `@main`) rather than a pinned commit SHA, a compromised upstream Action (via a hijacked maintainer account or a malicious release) could execute arbitrary code with the CI job's permissions the next time the workflow runs — a well-documented, real-world GitHub Actions supply-chain pattern.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage: M (job-scoped token, not repo-admin) · Reproducibility: L (depends on an upstream compromise, not attacker-controlled) · Exploitability: M (well-known technique, low skill once an upstream compromise occurs) · Affected users: this repository's CI only · Discoverability: M (workflow YAML is public if the repo is public)
- **Mapped frameworks:** CWE-829 (Inclusion of Functionality from Untrusted Control Sphere); OWASP Top 10 CI/CD Security Risks — CICD-SEC-3 (self-hosted/third-party runner and dependency risk)
- **Current mitigation status:** None — these two Actions are new to this pipeline.
- **Proposed action (party recommendation):** Mitigate now
  - Pin both new Actions to a full 40-character commit SHA in `dotnet.yml`, not a tag (e.g. `gitleaks/gitleaks-action@<sha>` with the tag as a trailing comment for readability), matching the standard GitHub-recommended hardening for third-party Actions.
    - **Testable acceptance criterion:** A structural test (or a CI lint step) asserts every `uses:` line referencing `gitleaks-action` or the Trivy action in `dotnet.yml` matches a 40-character hex SHA, not a bare tag or branch name.

### T-002 — A scan step could leak a real secret's value into public CI logs

- **STRIDE category:** Information Disclosure
- **Trust boundary:** TB-2
- **Asset affected:** Any real secret gitleaks matches during a scan (in the worst case, an actual live credential, not just the canary fixture)
- **Attack vector:** If gitleaks (or a misconfigured custom rule) prints the full matched string rather than a redacted excerpt when reporting a finding, that value lands in the job's log output — visible in the Actions UI, and to anyone with read access if the repository is public.
- **Severity:** HIGH (if it occurred, the leaked value would be as exposed as the original `ISSUE-002` credential, in a place harder to scrub than git history)
- **DREAD breakdown:** Damage: H (equivalent exposure to the incident this feature exists to prevent) · Reproducibility: L (gitleaks' default behavior does not do this; only a misconfiguration would) · Exploitability: L (requires a specific misconfiguration, not an external attacker action) · Affected users: whoever's credential is matched · Discoverability: M (CI logs are visible in the Actions UI)
- **Mapped frameworks:** CWE-532 (Insertion of Sensitive Information into Log File)
- **Current mitigation status:** Partial — the PRD's Non-Functional Requirements already state "No secret value may appear in CI logs even when a scan step fails," but this is currently an unverified statement, not a tested behavior.
- **Proposed action (party recommendation):** Mitigate now
  - Extend the canary-test acceptance criterion already in the PRD (AC 7) to also assert the canary test's own CI log output does not contain the literal fixture secret value — proving redaction empirically, not just configuring it and hoping.
    - **Testable acceptance criterion:** Given the gitleaks canary test runs against the Atlas-credential-shaped fixture, the job's captured log output contains the fixture's file path and line number but never the fixture's literal secret string.

### T-003 — A malicious transitive NuGet package executes code during the new per-service publish step

- **STRIDE category:** Elevation of Privilege
- **Trust boundary:** TB-3
- **Asset affected:** CI job's ambient token and compute, during `dotnet publish -t:PublishContainer` for each of the 7 services
- **Attack vector:** NuGet packages can run arbitrary code via MSBuild targets/install scripts at restore or build time. A compromised transitive dependency could exfiltrate CI secrets or tamper with the build during any of the 7 new per-service publish invocations.
- **Severity:** MEDIUM
- **DREAD breakdown:** Damage: M · Reproducibility: L (depends on an upstream package compromise) · Exploitability: M · Affected users: this repository's CI only · Discoverability: L
- **Mapped frameworks:** CWE-1357 (Reliance on Insufficiently Trustworthy Component)
- **Current mitigation status:** None specific to this feature — but this risk already exists today, unchanged in kind, for every existing `dotnet restore`/`dotnet build` invocation in `build-and-test`. This feature adds 7 more invocations of the same existing risk class; it does not introduce a new one.
- **Proposed action (party recommendation):** Accept
  - **Atlas's business justification:** fixing "arbitrary NuGet packages can execute code" is a generic .NET supply-chain concern out of scope for a CI/container-hardening feature — it would require a fundamentally different control (e.g., a package allowlist or `dotnet nuget verify`) applicable to the whole solution, not just the 7 services this feature touches.
  - **Phantom's residual-risk assessment:** no new exposure — the same risk already exists on every PR today via `build-and-test`, and this feature's dependency-audit job (Requirement 4) is itself a partial mitigation for the broader concern.

### T-004 — Resource exhaustion from a 7-way parallel container build+scan matrix

- **STRIDE category:** Denial of Service
- **Trust boundary:** TB-3
- **Asset affected:** GitHub Actions job-minute budget; CI turnaround time for unrelated PRs
- **Attack vector:** Every PR touching a service directory triggers 7 parallel `dotnet publish -t:PublishContainer` + Trivy scan runs. Combined with Trivy's uncached CVE-database download (already recorded in the PRD's Known Risks), this could slow or rate-limit CI on a busy day.
- **Severity:** LOW
- **DREAD breakdown:** Damage: L (cost/time, not data or integrity) · Reproducibility: M · Exploitability: L (no attacker benefit; only a reliability concern) · Affected users: this repository's own CI · Discoverability: L
- **Mapped frameworks:** CWE-400 (Uncontrolled Resource Consumption)
- **Current mitigation status:** Partial — the PRD already sets `timeout-minutes: 10` per matrix entry (Requirement 10), bounding the worst case.
- **Proposed action (party recommendation):** Accept
  - Already recorded in the PRD's Known Risks; the `timeout-minutes` bound is sufficient mitigation for a single-maintainer repository with no external contributors triggering CI at volume.

### T-005 — Dependabot PRs bypass review discipline

- **STRIDE category:** Repudiation
- **Trust boundary:** TB-4
- **Asset affected:** Dependency supply chain integrity
- **Attack vector:** If Dependabot PRs were auto-merged, a compromised upstream package version could land on `main` without human review.
- **Severity:** LOW
- **DREAD breakdown:** Damage: M (if it happened) · Reproducibility: L (requires auto-merge to be configured, which is not in this feature's scope) · Exploitability: L · Affected users: this repository · Discoverability: L
- **Mapped frameworks:** CWE-494 (Download of Code Without Integrity Check) — mitigated by the existing branch-protection review requirement
- **Current mitigation status:** Mitigated by existing control — `main` requires PR + human approval (`CONSTITUTION.md` §6), unchanged by this feature. No auto-merge is added.
- **Proposed action (party recommendation):** Accept
  - The existing PR-review requirement already covers Dependabot PRs identically to any other PR; this feature adds no auto-merge capability.

### T-006 — A workflow-file change weakens the new security gates themselves

- **STRIDE category:** Spoofing / Tampering
- **Trust boundary:** TB-3
- **Asset affected:** The integrity of the new `security-scan` and `docker-build-and-scan` jobs
- **Attack vector:** Anyone with PR access could propose a change to `dotnet.yml` that silently weakens or removes the new gates (e.g., changing a `fail` condition to a `warn`).
- **Severity:** LOW
- **DREAD breakdown:** Damage: M · Reproducibility: L · Exploitability: L · Affected users: this repository · Discoverability: L
- **Mapped frameworks:** CICD-SEC-2 (inadequate identity and access management for pipeline configuration)
- **Current mitigation status:** Mitigated by existing control — the same branch-protection + human-review requirement that governs every other change to this repository, unchanged and not introduced by this feature.
- **Proposed action (party recommendation):** Accept
  - Pre-existing, generic CI-governance risk that applies equally to every workflow change ever made in this repository, not specific to what F-017 adds.

---

## Threats Noted but Not Prioritized

*(None — all six identified threats were prioritized above; none were LOW-severity-and-undebated.)*

---

## Open Questions for Human

1. If this repository is or becomes public on GitHub, does the maintainer want an explicit policy for external (fork) pull requests before this feature ships — beyond the existing safe `pull_request` (not `pull_request_target`) trigger already in use? T-003's residual-risk acceptance assumes no active external contribution; that assumption should be confirmed.

---

## Approval Outcomes (filled in at Step 12)

*(Pending human review.)*

| Threat ID | Party recommendation | Human decision | Rationale |
|---|---|---|---|
| T-001 | Mitigate now | | |
| T-002 | Mitigate now | | |
| T-003 | Accept | | |
| T-004 | Accept | | |
| T-005 | Accept | | |
| T-006 | Accept | | |

**Tasks + security acceptance criteria to be created at Plan (Step 13):**

| Threat ID | Task | Testable `[security]` AC |
|---|---|---|
| T-001 | Pin `gitleaks-action` and the Trivy action to full commit SHAs in `dotnet.yml` | `[security] (T-001)` Every `uses:` line for the two new Actions matches a 40-character hex SHA, not a tag or branch. 🧪 test-first |
| T-002 | Extend the gitleaks canary test to assert log redaction | `[security] (T-002)` Given the gitleaks canary test runs against the Atlas-credential-shaped fixture, the job's captured log output contains the file path and line number but never the fixture's literal secret string. 🧪 test-first |

---

## Revision History

| Date | Author | Change |
|---|---|---|
| 2026-08-25 | Phantom (initial draft, solo mode) | Created at Step 10.5 — Full triage (2/3), 6 threats identified, 2 mitigate-now, 4 accept |
