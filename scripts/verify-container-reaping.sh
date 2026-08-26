#!/usr/bin/env bash
# verify-container-reaping.sh — F-018-T15 / AC-13.
#
# Proves that Testcontainers' resource reaper (Ryuk) actually cleans up an orphaned container
# after a real, abnormal exit — empirically, by performing the exact kill and watching what
# happens, not by trusting Testcontainers' own documentation of how Ryuk is supposed to behave.
# This is deliberately the same "reasoned, not observed" trap called out on the task: an earlier
# feature (F-013) got a threat wrong by reasoning about behavior instead of watching it happen.
#
# Procedure:
#   1. Launch a real AgendaBuddy.IntegrationTests test class that starts a MongoDB Testcontainer
#      through the project's own ServiceHostFixture<T> (Harness/ServiceHostFixture.cs) — the same
#      path every integration test uses, not a purpose-built fixture.
#   2. Watch the test process's own console output for Testcontainers' "Docker container <id>
#      ready" log lines. The first belongs to Ryuk (the reaper container Testcontainers always
#      starts first, to register everything that follows under its session); the second is the
#      Mongo container this test actually asked for.
#   3. The instant the Mongo container reports ready — before the test body runs, before
#      IAsyncLifetime.DisposeAsync ever gets a chance to remove the container the ordinary way —
#      SIGKILL the *entire* process tree the test is running in (dotnet test -> vstest.console ->
#      testhost.dll, discovered dynamically; PIDs differ every run). No graceful shutdown path
#      runs anywhere in that tree. This is the actual mechanism under test: what happens when
#      nothing at all gets a chance to clean up.
#   4. Poll `docker inspect` for the exact Mongo container ID (never a blind `docker ps` diff,
#      since this machine routinely has other Testcontainers sessions — other agents' test runs —
#      creating and destroying unrelated containers concurrently; only this run's own container
#      IDs are a meaningful signal here) until it disappears, and report how long that actually
#      took. Same for the Ryuk container itself, which should self-terminate shortly after.
#   5. Fail loudly, naming the surviving container, if either is still running after a generous
#      wait — this is the one outcome that must never be assumed away.
#
# Run standalone: ./scripts/verify-container-reaping.sh
# Wired into CI as a step in the `integration` job (.github/workflows/dotnet.yml), which is the
# only job that already has Docker + a built AgendaBuddy.IntegrationTests to reuse.

set -uo pipefail

export PATH="$HOME/.rd/bin:$PATH"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOGFILE="$(mktemp)"
trap 'rm -f "$LOGFILE"' EXIT

fail() {
  echo "❌ $1"
  echo "--- captured test process output ---"
  cat "$LOGFILE"
  exit 1
}

echo "Preflight: confirming a container runtime is reachable..."
if ! command -v docker >/dev/null 2>&1; then
  fail "docker is not on PATH (Rancher Desktop's CLI lives in ~/.rd/bin — this script already adds that to PATH; is Rancher Desktop installed?)."
fi
if ! docker info >/dev/null 2>&1; then
  fail "docker info failed — the container runtime is not reachable. Start Rancher Desktop (or the CI runner's Docker daemon) and retry."
fi

echo "Launching a real ServiceHostFixture-backed integration test in the background (PID will be reported)..."
dotnet test "$REPO_ROOT/AgendaBuddy.IntegrationTests/AgendaBuddy.IntegrationTests.csproj" \
  --filter "FullyQualifiedName~AgendaBuddy.IntegrationTests.Harness.ProfessionHostTest" \
  -p:MobileWorkloads=false \
  --logger "console;verbosity=detailed" \
  >"$LOGFILE" 2>&1 &
TEST_PID=$!
echo "Test process launched as PID $TEST_PID."

echo "Waiting for the Mongo container's readiness log line (Ryuk's own container reports ready first)..."

MONGO_ID=""
RYUK_ID=""
MAX_ITERATIONS=1200 # 1200 * 0.05s = 60s — generous for a cold mongo:7.0 pull (measured ~60s cold, ~1-5s warm)
iteration=0
while [ "$iteration" -lt "$MAX_ITERATIONS" ]; do
  ready_lines="$(grep -oE 'Docker container [0-9a-f]+ ready' "$LOGFILE" 2>/dev/null || true)"
  ready_count="$(printf '%s\n' "$ready_lines" | grep -c . || true)"
  ready_count="${ready_count:-0}"

  if [ "$ready_count" -ge 2 ]; then
    RYUK_ID="$(printf '%s\n' "$ready_lines" | sed -n '1p' | awk '{print $3}')"
    MONGO_ID="$(printf '%s\n' "$ready_lines" | sed -n '2p' | awk '{print $3}')"
    break
  fi

  if ! kill -0 "$TEST_PID" 2>/dev/null; then
    fail "the test process exited before its Mongo container reported ready — there was nothing to kill mid-flight. See captured output."
  fi

  sleep 0.05
  iteration=$((iteration + 1))
done

if [ -z "$MONGO_ID" ]; then
  kill -9 "$TEST_PID" 2>/dev/null || true
  fail "timed out (60s) waiting for the Mongo container's readiness log line. See captured output."
fi

case "$RYUK_ID" in
[0-9a-f]*) : ;;
*)
  kill -9 "$TEST_PID" 2>/dev/null || true
  fail "parsed Ryuk container ID '$RYUK_ID' does not look like a container ID — the log-parsing regex broke. Fix before trusting any result from this script."
  ;;
esac
case "$MONGO_ID" in
[0-9a-f]*) : ;;
*)
  kill -9 "$TEST_PID" 2>/dev/null || true
  fail "parsed Mongo container ID '$MONGO_ID' does not look like a container ID — the log-parsing regex broke. Fix before trusting any result from this script."
  ;;
esac

echo "Ryuk container: $RYUK_ID"
echo "Mongo container: $MONGO_ID (ready — killing the test process tree mid-flight NOW)"

# Discover the live descendant tree of $TEST_PID at this exact instant. `dotnet test` launches
# vstest.console via `dotnet exec`, which in turn launches testhost.dll via `dotnet exec` — the
# testhost process is the one actually running the C# test body and holding the Testcontainers
# client's connection to Ryuk. PIDs are not stable across runs, so this is discovered live rather
# than assumed.
collect_tree() {
  local pid="$1"
  echo "$pid"
  local child
  for child in $(pgrep -P "$pid" 2>/dev/null || true); do
    collect_tree "$child"
  done
}

pids_to_kill="$(collect_tree "$TEST_PID")"
kill_time=$(date +%s)

for pid in $pids_to_kill; do
  kill -9 "$pid" 2>/dev/null || true
done

echo "SIGKILLed the following PIDs (the whole tree, all at once — no graceful shutdown anywhere in it):"
echo "$pids_to_kill" | tr '\n' ' '
echo

# Reap our own child so it doesn't linger as a zombie under this script.
wait "$TEST_PID" 2>/dev/null || true

echo "Test process tree is dead. Polling docker for the Mongo container's removal (this is the actual proof — not a fixed sleep)..."

mongo_reaped=false
for _ in $(seq 1 60); do
  if ! docker inspect "$MONGO_ID" >/dev/null 2>&1; then
    mongo_reaped=true
    break
  fi
  sleep 1
done
mongo_reap_seconds=$(($(date +%s) - kill_time))

if [ "$mongo_reaped" != true ]; then
  fail "orphan container $MONGO_ID (mongo:7.0) is STILL RUNNING ${mongo_reap_seconds}s after the test process was SIGKILLed. Ryuk did NOT reap it — container reaping after an abnormal exit is BROKEN on this machine. Remove it manually: docker rm -f $MONGO_ID"
fi

echo "Mongo container $MONGO_ID was reaped ${mongo_reap_seconds}s after the abnormal kill."

ryuk_gone=false
for _ in $(seq 1 30); do
  if ! docker inspect "$RYUK_ID" >/dev/null 2>&1; then
    ryuk_gone=true
    break
  fi
  sleep 1
done
ryuk_gone_seconds=$(($(date +%s) - kill_time))

if [ "$ryuk_gone" != true ]; then
  fail "the Ryuk reaper container $RYUK_ID is itself still running ${ryuk_gone_seconds}s after reaping — it should self-terminate once its session's work is done. Remove it manually: docker rm -f $RYUK_ID"
fi

echo "Ryuk container $RYUK_ID self-terminated ${ryuk_gone_seconds}s after the abnormal kill."
echo
echo "✅ T015_ContainerReapingSurvivesAnAbnormalKill: a real SIGKILL of the integration test's entire"
echo "   process tree mid-flight — after the Mongo container ($MONGO_ID) reported ready but before"
echo "   ServiceHostFixture's own DisposeAsync (or anything else) could run — still resulted in both"
echo "   the Mongo container and its Ryuk guard ($RYUK_ID) being removed within ${mongo_reap_seconds}s"
echo "   and ${ryuk_gone_seconds}s respectively. Ryuk's resource reaper is proven, by direct"
echo "   observation, to actually reap on this machine (F-018-T15 / AC-13)."
