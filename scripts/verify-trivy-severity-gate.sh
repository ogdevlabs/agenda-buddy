#!/usr/bin/env bash
# verify-trivy-severity-gate.sh — F-017-T07 / Review C1 (Party Review, 2026-08-26).
#
# Proves trivy-severity-gate.sh's severity-gate branching logic, empirically, against synthetic
# Trivy `--format json` report fixtures — not just by reading the jq queries. This is the
# regression coverage AC9 was missing: the branching was verified once by hand during Construction,
# never committed as a repeatable check.
#
# Four fixtures, four expected outcomes:
#   1. base-image-only HIGH        -> exit 0, warns
#   2. project-introduced CRITICAL -> exit 1, fails
#   3. mixed (both)                -> exit 1 (project finding still fails the gate)
#   4. clean (neither)              -> exit 0, no warning, no error
#
# Run standalone: ./scripts/verify-trivy-severity-gate.sh
# Not wired into a per-service CI matrix leg (it's about the gate's generic logic, not any one
# service's real scan) — runs once as a step in the security-scan job, alongside the gitleaks
# canary, so both "does our own security tooling actually work" proofs live together.

set -uo pipefail

GATE_SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/trivy-severity-gate.sh"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

fail() {
  echo "❌ $1"
  exit 1
}

vulnerability() {
  local id="$1" severity="$2" pkg="$3"
  printf '{"VulnerabilityID":"%s","Severity":"%s","PkgName":"%s"}' "$id" "$severity" "$pkg"
}

report() {
  local target="$1" vuln_json="$2"
  cat <<EOF
{"Results":[{"Target":"$target","Vulnerabilities":[$vuln_json]}]}
EOF
}

check() {
  local name="$1" report_file="$2" expected_exit="$3" expect_warning="$4" expect_error="$5"

  set +e
  output=$("$GATE_SCRIPT" "$report_file" 2>&1)
  actual_exit=$?
  set -e

  if [ "$actual_exit" -ne "$expected_exit" ]; then
    echo "--- captured output ---"
    echo "$output"
    fail "$name: expected exit $expected_exit, got $actual_exit."
  fi

  if [ "$expect_warning" = "yes" ] && ! echo "$output" | grep -q "::warning::"; then
    fail "$name: expected a ::warning:: line for the base-image-inherited finding, none printed."
  fi
  if [ "$expect_warning" = "no" ] && echo "$output" | grep -q "::warning::"; then
    fail "$name: printed a ::warning:: when none was expected."
  fi

  if [ "$expect_error" = "yes" ] && ! echo "$output" | grep -q "::error::"; then
    fail "$name: expected an ::error:: line for the project-introduced finding, none printed."
  fi
  if [ "$expect_error" = "no" ] && echo "$output" | grep -q "::error::"; then
    fail "$name: printed an ::error:: when none was expected."
  fi

  echo "  ✓ $name"
}

# Fixture 1: base-image-only HIGH — target is the OS layer, not app/*.deps.json.
report "profession:latest (ubuntu 24.04)" "$(vulnerability CVE-2024-0001 HIGH openssl)" \
  > "$WORKDIR/base-only-high.json"

# Fixture 2: project-introduced CRITICAL — target matches app/<Service>.deps.json.
report "app/Profession.deps.json" "$(vulnerability CVE-2024-0002 CRITICAL Newtonsoft.Json)" \
  > "$WORKDIR/project-only-critical.json"

# Fixture 3: mixed — both a base-image finding and a project finding in the same report.
cat > "$WORKDIR/mixed.json" <<EOF
{"Results":[
  {"Target":"profession:latest (ubuntu 24.04)","Vulnerabilities":[$(vulnerability CVE-2024-0001 HIGH openssl)]},
  {"Target":"app/Profession.deps.json","Vulnerabilities":[$(vulnerability CVE-2024-0002 CRITICAL Newtonsoft.Json)]}
]}
EOF

# Fixture 4: clean — no HIGH/CRITICAL anywhere.
report "app/Profession.deps.json" "$(vulnerability CVE-2024-0003 LOW SomePackage)" \
  > "$WORKDIR/clean.json"

echo "Verifying trivy-severity-gate.sh against synthetic fixtures:"
check "base-image-only HIGH warns, does not fail"     "$WORKDIR/base-only-high.json"      0 yes no
check "project-introduced CRITICAL fails"             "$WORKDIR/project-only-critical.json" 1 no  yes
check "mixed: project finding still fails the gate"   "$WORKDIR/mixed.json"               1 yes yes
check "clean report: no warning, no error, exit 0"    "$WORKDIR/clean.json"               0 no  no

echo "✅ T009_TrivySeverityGateDistinguishesProjectFindingsFromBaseImageFindings: all 4 synthetic fixtures produced the expected pass/warn/fail outcome."
