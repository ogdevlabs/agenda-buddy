#!/usr/bin/env bash
# verify-gitleaks-canary.sh — F-017-T05.
#
# Proves two things about the gitleaks configuration in .gitleaks.toml, empirically, not by
# inspection:
#   1. (PRD AC 7)  The configured ruleset actually detects a fixture matching the SHAPE of the
#      credential this project already leaked once (ISSUE-002) — a synthetic MongoDB/Atlas
#      connection string, never the real value.
#   2. ([security] AC 15, threat-model.md T-002)  When it's detected, the captured log output
#      contains the fixture's file path and line number but NEVER the fixture's literal secret
#      value — proving redaction empirically, the same way gitleaks-action actually invokes
#      gitleaks in CI (--redact -v --report-format=sarif), not a hypothetical invocation.
#
# The fixture is generated into a throwaway temp directory and deleted on exit — it is never
# committed to the repo. Committing a file gitleaks is *meant* to flag would make the real
# security-scan job's gitleaks step fail on every PR, forever, once merged.
#
# Run standalone: ./scripts/verify-gitleaks-canary.sh
# Wired into CI as its own step in the security-scan job (.github/workflows/dotnet.yml).

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

FIXTURE_FILE="$WORKDIR/atlas-credential-canary.txt"
FAKE_PASSWORD="Xk7pQm2vTz9wLc4RnBs8"
echo "mongodb+srv://agenda_buddy:${FAKE_PASSWORD}@agenda-buddy-cluster.a1b2c.mongodb.net/agenda_buddy?retryWrites=true&w=majority" > "$FIXTURE_FILE"

SARIF_PATH="$WORKDIR/results.sarif"

# Same flags gitleaks-action actually passes (verified against gitleaks/gitleaks-action's
# src/gitleaks.js Scan() function) — this script exercises the real invocation shape, not a
# simplified stand-in for it.
OUTPUT=$(gitleaks detect \
  --source "$WORKDIR" \
  --no-git \
  --no-banner \
  --config "$REPO_ROOT/.gitleaks.toml" \
  --redact \
  -v \
  --exit-code=2 \
  --report-format=sarif \
  --report-path="$SARIF_PATH" \
  --log-level=debug 2>&1)
EXIT_CODE=$?

fail() {
  echo "❌ $1"
  echo "--- captured gitleaks output ---"
  echo "$OUTPUT"
  exit 1
}

# AC 7: the ruleset must actually detect the fixture (gitleaks-action's own exit code for
# "leaks detected" is 2, not the CLI default of 1).
if [ "$EXIT_CODE" -ne 2 ]; then
  fail "Expected exit code 2 (leaks detected), got $EXIT_CODE — the configured ruleset did not flag the Atlas-credential-shaped fixture."
fi

# [security] AC 15 / T-002, part 1: file path and line number must be present.
if ! echo "$OUTPUT" | grep -q "atlas-credential-canary.txt"; then
  fail "Captured output does not contain the fixture's file path — redaction proof requires the location to survive."
fi
if ! echo "$OUTPUT" | grep -q "Line:.*1"; then
  fail "Captured output does not contain the fixture's line number."
fi

# [security] AC 15 / T-002, part 2 (the critical negative assertion): the literal secret value
# must never appear, in the console output OR the SARIF report gitleaks-action uploads as an
# artifact and reads to post PR review comments.
if echo "$OUTPUT" | grep -q "$FAKE_PASSWORD"; then
  fail "Captured console output contains the fixture's literal secret value — redaction failed."
fi
if [ -f "$SARIF_PATH" ] && grep -q "$FAKE_PASSWORD" "$SARIF_PATH"; then
  fail "SARIF report contains the fixture's literal secret value — redaction failed in the artifact gitleaks-action uploads."
fi

echo "✅ T002_GitleaksRedactsSecretValueFromCapturedLogOutput: gitleaks detected the Atlas-credential-shaped canary (AC 7) and redacted its value from both console output and the SARIF report while preserving file:line (AC 15 / T-002)."
