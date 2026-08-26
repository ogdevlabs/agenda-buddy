# Wave Kickoff Standup — F-017 Wave 3

**Called by:** Neo (Architect)
**Mode:** Solo (same rationale as Wave 2 — 2-task wave, direct file-region inspection substitutes for a full panel)
**Purpose:** Coordination check before claiming F-017-T05 and F-017-T07
**Date:** 2026-08-26

---

## Wave 3 tasks

- **F-017-T05** (depends on T04, done) — canary test proving gitleaks catches an Atlas-credential-shaped fixture, plus the T-002 log-redaction proof (PRD AC15, `[security]`).
- **F-017-T07** (depends on T06, done) — Trivy scan step in `docker-build-and-scan`, severity-gated (project-introduced HIGH/CRITICAL fails, base-image-inherited warns).

## File-overlap check

Current job layout: `changes:17`, `build-and-test:183`, `security-scan:328`, `docker-build-and-scan:401` (ends ~431, steps: checkout/setup-dotnet/nuget-cache/publish), `integration:456`, ... `summary:630`.

- T05 edits inside/near `security-scan` (328-400) plus new fixture file(s) in the repo.
- T07 edits inside `docker-build-and-scan` (401-431) plus the `summary` job (same pattern T06 used for its own job).

No overlap. Both genuinely testable locally, not infra-only-exception candidates:
- `gitleaks` is already installed locally (Homebrew, confirmed by T04's agent) — T05's canary + redaction proof can be a real red→green local verification, no TDD override needed.
- `trivy` is not yet installed but installable via Homebrew; Docker is available (Rancher Desktop, confirmed running). T07 can build a real image locally (`dotnet publish -t:PublishContainer`) and scan it with a locally-installed Trivy — no override needed.

## Wave Execution Plan

1. **Confirmed safe parallel tasks:** T05, T07 — separate worktrees.
2. **Flagged sequential pairs:** none.
3. **Scope notes carried into task prompts:**
   - T05 carries a `[security]` PRD AC (AC15/T-002) — per the TDD gate, this needs its own dedicated test, named after the threat ID, not covered by an adjacent happy-path test.
   - T07's severity-gate logic (project-introduced fails, base-image-inherited warns) should be verified against both a real scanned image AND synthetic fixture cases, mirroring T03's dependency-audit filter verification approach.
4. **Dependency updates:** none needed.

**Recommended order:** parallel, worktree-isolated.
