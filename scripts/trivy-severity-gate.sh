#!/usr/bin/env bash
# F-017-T07: severity-gate filter for a Trivy `--format json` image scan report.
#
# Trivy's own report distinguishes findings by `.Results[].Target`. The project's own
# published dependencies always land under a target named `app/<Project>.deps.json` — a path
# that only exists in the built image, never in the bare base image
# (mcr.microsoft.com/dotnet/aspnet:10.0) it's built from. Every other target (the OS-package
# layer, and the shared-framework `deps.json` files under usr/share/dotnet/) is inherited from
# the base image and this project cannot fix a CVE in it directly.
#
# Usage: trivy-severity-gate.sh <trivy-report.json>
# Exit 0 with a warning printed for base-image-inherited HIGH/CRITICAL findings.
# Exit 1 for any HIGH/CRITICAL finding under an app/*.deps.json target.
set -euo pipefail

report="$1"

project_findings=$(jq -r '
  .Results[]?
  | select(.Target | test("^app/.*\\.deps\\.json$"))
  | .Vulnerabilities[]?
  | select(.Severity == "HIGH" or .Severity == "CRITICAL")
  | "\(.VulnerabilityID) \(.Severity) \(.PkgName)"
' "$report")

base_image_findings=$(jq -r '
  .Results[]?
  | select(.Target | test("^app/.*\\.deps\\.json$") | not)
  | .Target as $target
  | .Vulnerabilities[]?
  | select(.Severity == "HIGH" or .Severity == "CRITICAL")
  | "\($target): \(.VulnerabilityID) \(.Severity) \(.PkgName)"
' "$report")

if [ -n "$base_image_findings" ]; then
  echo "::warning::Base-image-inherited HIGH/CRITICAL finding(s) — not failing (unfixable by this project directly):"
  echo "$base_image_findings"
fi

if [ -n "$project_findings" ]; then
  echo "::error::Project-introduced HIGH/CRITICAL finding(s) in this project's own dependencies:"
  echo "$project_findings"
  exit 1
fi

echo "No project-introduced HIGH/CRITICAL findings."
