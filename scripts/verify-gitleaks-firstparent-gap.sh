#!/usr/bin/env bash
# verify-gitleaks-firstparent-gap.sh
#
# Proves, empirically and deterministically, both halves of the gap this project's CI works
# around: gitleaks-action's own PR-diff scan always runs `--no-merges --first-parent`, which
# never walks a merge commit's second parent — exactly where a worktree-agent's actual code
# lands when merged with `git merge --no-ff`. This script builds that exact shape in a
# throwaway repo (a spine commit with no secret, merged --no-ff with a branch whose only commit
# carries a secret), then asserts:
#   1. gitleaks-action's own invocation (--no-merges --first-parent) finds nothing.
#   2. This project's full-range step's invocation (no --first-parent) finds the secret.
# If either assertion flips, the gap has changed shape (or gitleaks/gitleaks-action fixed it
# upstream) and this script — not a live PR — is the place that should catch it.
#
# Run standalone: ./scripts/verify-gitleaks-firstparent-gap.sh

set -uo pipefail

GITLEAKS_BIN="${GITLEAKS_BIN:-gitleaks}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

fail() {
  echo "❌ $1"
  exit 1
}

git init -q "$WORKDIR"
git -C "$WORKDIR" config user.name "gitleaks-gap-canary"
git -C "$WORKDIR" config user.email "canary@example.invalid"

# A real PR's base always has ancestor history, so gitleaks-action's own `baseRef^..headRef`
# syntax always resolves. This repo's first-ever commit has no parent, so BASE_SHA must not be
# the root commit or `BASE_SHA^` is invalid git syntax — an artifact of this throwaway repo, not
# of the real gap. Seed a root commit, then treat the one after it as BASE_SHA.
echo "root" > "$WORKDIR/root.txt"
git -C "$WORKDIR" add root.txt
git -C "$WORKDIR" commit -q -m "root (seed, so BASE_SHA has a parent)"

echo "base" > "$WORKDIR/base.txt"
git -C "$WORKDIR" add base.txt
git -C "$WORKDIR" commit -q -m "base"
BASE_SHA=$(git -C "$WORKDIR" rev-parse HEAD)

git -C "$WORKDIR" checkout -q -b spine
echo "spine, no secret" > "$WORKDIR/spine.txt"
git -C "$WORKDIR" add spine.txt
git -C "$WORKDIR" commit -q -m "spine commit, no secret"

git -C "$WORKDIR" checkout -q "$BASE_SHA" -b secret-branch
FAKE_PASSWORD="Qw8mNp3xZr6vTy1LcAe9Fj4" # gitleaks:allow — synthetic fixture value, not a real secret
echo "mongodb+srv://canary_gap_user:${FAKE_PASSWORD}@canary-gap-cluster.z9y8x.mongodb.net/canary_gap?retryWrites=true&w=majority" > "$WORKDIR/secret.txt"
git -C "$WORKDIR" add secret.txt
git -C "$WORKDIR" commit -q -m "second-parent-only commit, carries the secret"
SECRET_SHA=$(git -C "$WORKDIR" rev-parse HEAD)

git -C "$WORKDIR" checkout -q spine
git -C "$WORKDIR" merge -q --no-ff secret-branch -m "merge secret-branch (--no-ff)"
HEAD_SHA=$(git -C "$WORKDIR" rev-parse HEAD)

# Assertion 1 — gitleaks-action's own invocation shape must miss the secret.
FIRSTPARENT_OUTPUT=$("$GITLEAKS_BIN" detect \
  --source "$WORKDIR" \
  --config "$REPO_ROOT/.gitleaks.toml" \
  --log-opts="--no-merges --first-parent ${BASE_SHA}^..${HEAD_SHA}" \
  --redact -v --exit-code=2 2>&1)
FIRSTPARENT_EXIT=$?

if [ "$FIRSTPARENT_EXIT" -ne 0 ]; then
  fail "Expected --first-parent scan to find nothing (exit 0), got $FIRSTPARENT_EXIT — the gap this project works around may have already been fixed upstream, or the branch shape is wrong. Output:
$FIRSTPARENT_OUTPUT"
fi

# Assertion 2 — this project's full-range step's invocation shape must catch it.
FULLRANGE_OUTPUT=$("$GITLEAKS_BIN" detect \
  --source "$WORKDIR" \
  --config "$REPO_ROOT/.gitleaks.toml" \
  --log-opts="${BASE_SHA}..${HEAD_SHA}" \
  --redact -v --exit-code=1 2>&1)
FULLRANGE_EXIT=$?

if [ "$FULLRANGE_EXIT" -ne 1 ]; then
  fail "Expected the full-range scan to find the second-parent-only secret (exit 1), got $FULLRANGE_EXIT — the full-range step would not have caught this. Output:
$FULLRANGE_OUTPUT"
fi
if ! echo "$FULLRANGE_OUTPUT" | grep -q "mongodb-atlas-connection-string"; then
  fail "Full-range scan failed for a reason other than the expected secret — got a different rule or no rule ID in output:
$FULLRANGE_OUTPUT"
fi

echo "✅ gitleaks-action's --first-parent invocation misses a secret behind a --no-ff merge's second parent (0 leaks); this project's full-range step catches it (1 leak, mongodb-atlas-connection-string). The gap and the fix both still hold their expected shape."
