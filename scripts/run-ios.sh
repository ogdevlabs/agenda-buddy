#!/usr/bin/env bash
# run-ios.sh — one command: bring up the whole backend, then launch AgendaBuddy.MobileApp on an iOS simulator.
#
#   ./scripts/run-ios.sh                        # newest available iPhone simulator
#   ./scripts/run-ios.sh --device "iPhone 17"   # a named simulator
#   ./scripts/run-ios.sh --list-devices         # what simulators exist
#   ./scripts/run-ios.sh --skip-apphost         # backend already running elsewhere
#   ./scripts/run-ios.sh --no-app               # backend only: start it, print the ports, hold
#
# Ctrl-C shuts the AppHost down. The simulator is left booted (booting one is slow; reusing it is the
# whole point of leaving it up).
#
# ─────────────────────────────────────────────────────────────────────────────────────────────────
# WHAT THIS SCRIPT CANNOT DO FOR YOU, AND WHY
#
# 1. THE APP CAN NOW REACH A GATEWAY, BUT NOT EVERY ROUTE THROUGH IT YET. F-015-T12 closed the base-
#    address half of this: this script discovers the Gateway's dynamically-assigned port the same way
#    it already discovers the seven services' (see WHY PORTS ARE DISCOVERED, below), and injects it
#    into the simulator process as the MAUI_API_BASE_URL environment variable, which
#    AgendaBuddy.MobileApp/Infrastructure/ApiBaseUrlResolver.cs (read from MauiProgram.cs) now prefers over the
#    `ApiBaseUrl` configuration key (still never populated — nothing loads AgendaBuddy.MobileApp/appsettings.json)
#    and the hardcoded `http://localhost:6036/` fallback, which addresses nothing under the AppHost
#    (F-013 removed the fixed 603x ports, AC-1.4). What F-015-T12 does NOT fix: every mobile path
#    still needs the `api/v1/` prefix the backend actually serves (F-015-T07, Planned). Until that
#    lands, calls through the now-correctly-addressed gateway still 404, and the ViewModels still fall
#    back to AgendaBuddy.MobileApp/Services/SeedDataProvider.cs. See docs/pdlc/context/01-api-surface.md and
#    docs/pdlc/context/16-mobile-client.md.
#
# 2. TWO XCODE STEPS NEED sudo, so they are checked and explained, never performed:
#    accepting the Xcode license, and pointing xcode-select at Xcode.app. The second is worked
#    around here without sudo by exporting DEVELOPER_DIR, which xcrun and the .NET iOS targets both
#    honour; the license genuinely cannot be.
#
# WHY PORTS ARE DISCOVERED INSTEAD OF ASSUMED
#
# The AppHost assigns every service a random host port on purpose (AppHostWiring.cs clears each
# EndpointAnnotation's Port and TargetPort). Nothing prints those ports to stdout, so they are
# recovered from the OS: find each service process, list what it listens on, and ask each candidate
# port for /alive. Each service binds both an HTTP and an HTTPS endpoint and only the HTTP one
# answers plaintext, which is what the 200 check settles. The Gateway (F-015-T05, the eighth AppHost
# resource) is found the exact same way — it is one more entry probed by the same loop, not a new
# discovery mechanism — and its port is the one difference from the seven services: it is also
# injected into the simulator launch as MAUI_API_BASE_URL (F-015-T12), not just printed for a human.
#
# Requires: dotnet, docker (Rancher Desktop: docker lives in ~/.rd/bin, added to PATH below),
#           curl, lsof, python3, Xcode + an iOS simulator runtime, and the maui workload.
# ─────────────────────────────────────────────────────────────────────────────────────────────────

set -euo pipefail

# Rancher Desktop puts docker outside the default PATH; Aspire shells out to it. Harmless if present.
export PATH="$HOME/.rd/bin:$PATH"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APPHOST_LOG="${TMPDIR:-/tmp}/agenda-buddy-apphost.log"
SERVICES=(Identity Booking.Api Customer Provider Calendar Services Profession)
GATEWAY="Gateway"         # the eighth AppHost resource (F-015-T05) — project dir matches AppHostWiring.cs's Projects.Gateway
READY_TIMEOUT=300          # a cold run builds seven services first
XCODE_APP="/Applications/Xcode.app"

device_name=""
skip_apphost=0
run_app=1
list_devices=0
apphost_pid=""
started_apphost=0

while [ "$#" -gt 0 ]; do
  case "$1" in
    --device)        device_name="${2:-}"; shift 2 ;;
    --device=*)      device_name="${1#*=}"; shift ;;
    --skip-apphost)  skip_apphost=1; shift ;;
    --no-app)        run_app=0; shift ;;
    --list-devices)  list_devices=1; shift ;;
    -h|--help)       sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *)               echo "unknown argument: $1 (try --help)" >&2; exit 2 ;;
  esac
done

die() { echo "error: $*" >&2; exit 1; }
say() { echo "==> $*"; }

cleanup() {
  local code=$?
  if [ "$started_apphost" = "1" ] && [ -n "$apphost_pid" ]; then
    say "stopping the AppHost (pid $apphost_pid)"
    kill "$apphost_pid" 2>/dev/null || true
    wait "$apphost_pid" 2>/dev/null || true
  fi
  exit $code
}
trap cleanup EXIT INT TERM

# ── Xcode ───────────────────────────────────────────────────────────────────────────────────────
# The .NET iOS targets and xcrun both prefer DEVELOPER_DIR over the xcode-select default, so a
# machine left pointing at CommandLineTools can be steered here rather than with `sudo xcode-select`.
xcode_selected() {
  case "$(xcode-select -p 2>/dev/null)" in
    *Xcode*) return 0 ;;
    *)       return 1 ;;
  esac
}

setup_xcode() {
  if [ -z "${DEVELOPER_DIR:-}" ] && ! xcode_selected; then
    [ -d "$XCODE_APP" ] || die "Xcode is not installed at $XCODE_APP — the iOS simulator needs the full Xcode, not Command Line Tools."
    export DEVELOPER_DIR="$XCODE_APP/Contents/Developer"
    say "xcode-select points at Command Line Tools; using DEVELOPER_DIR=$DEVELOPER_DIR for this run"
    echo "    to make it permanent (needs sudo, run it yourself):  sudo xcode-select -s $XCODE_APP"
  fi

  # simctl is missing from a Command Line Tools install and is also the first thing to fail when the
  # licence has not been accepted, so its error text is worth surfacing verbatim.
  local probe
  if ! probe="$(xcrun simctl help 2>&1)"; then
    case "$probe" in
      *license*)
        die "Xcode's licence has not been accepted, so simctl will not run. Run this yourself, then re-run:
       sudo xcodebuild -license accept" ;;
      *)
        die "xcrun simctl is unavailable:
       $probe" ;;
    esac
  fi

  dotnet workload list 2>/dev/null | grep -q '^maui' \
    || die "the 'maui' workload is not installed — run: dotnet workload install maui"

  # A fresh Xcode ships the iOS SDK but no simulator runtime, and without a runtime simctl reports
  # zero devices — which reads like "wrong device name" rather than "nothing to run on". Say so.
  if ! xcrun simctl list runtimes 2>/dev/null | grep -q 'iOS'; then
    die "no iOS simulator runtime is installed, so there are no simulators to launch. Install one
       (several GB, and it may ask for your password), then re-run:
         xcodebuild -downloadPlatform iOS
       or Xcode > Settings > Components > iOS Simulator."
  fi
}

# ── Backend ─────────────────────────────────────────────────────────────────────────────────────
pid_of_service() {
  ps -Ao pid=,command= \
    | awk -v pat="$REPO_ROOT/$1/bin/" 'index($0, pat) { print $1; exit }'
}

# The HTTP endpoint of a service, found by asking each port it listens on for /alive. The HTTPS
# endpoint answers a plaintext request with 400, which is exactly the discriminator wanted here.
http_port_of_pid() {
  local pid="$1" port
  for port in $(lsof -nP -iTCP -sTCP:LISTEN -a -p "$pid" 2>/dev/null \
                  | awk 'NR>1 { n = split($9, a, ":"); print a[n] }' | sort -u); do
    if [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 2 "http://127.0.0.1:$port/alive")" = "200" ]; then
      echo "$port"; return 0
    fi
  done
  return 1
}

start_apphost() {
  if pgrep -f 'AgendaBuddy.AppHost' >/dev/null 2>&1; then
    say "an AppHost is already running — reusing it (it will be left alone on exit)"
    return
  fi
  say "starting the AppHost (log: $APPHOST_LOG)"
  : > "$APPHOST_LOG"
  dotnet run --project "$REPO_ROOT/AgendaBuddy.AppHost" > "$APPHOST_LOG" 2>&1 &
  apphost_pid=$!
  started_apphost=1
}

# Populates PORTS with "Service port" lines and GATEWAY_PORT with the gateway's port, or fails once
# READY_TIMEOUT is spent. The gateway is probed with the exact same http_port_of_pid/pid_of_service
# mechanism as the seven services (F-015-T12) — it is just one more entry, not a new discovery path.
wait_for_services() {
  local waited=0 found gw_pid gw_port
  say "waiting for all ${#SERVICES[@]} services and the gateway to answer /health (up to ${READY_TIMEOUT}s; a cold run builds first)"
  while [ "$waited" -lt "$READY_TIMEOUT" ]; do
    found=""
    for svc in "${SERVICES[@]}"; do
      local pid port
      pid="$(pid_of_service "$svc")" || true
      [ -n "$pid" ] || continue
      port="$(http_port_of_pid "$pid")" || continue
      [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "http://127.0.0.1:$port/health")" = "200" ] || continue
      found+="$svc $port"$'\n'
    done

    gw_port=""
    gw_pid="$(pid_of_service "$GATEWAY")" || true
    if [ -n "$gw_pid" ]; then
      port="$(http_port_of_pid "$gw_pid")" || true
      if [ -n "${port:-}" ] && [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "http://127.0.0.1:$port/health")" = "200" ]; then
        gw_port="$port"
      fi
    fi

    if [ "$(printf '%s' "$found" | grep -c .)" = "${#SERVICES[@]}" ] && [ -n "$gw_port" ]; then
      PORTS="$found"
      GATEWAY_PORT="$gw_port"
      return 0
    fi
    if [ "$started_apphost" = "1" ] && ! kill -0 "$apphost_pid" 2>/dev/null; then
      echo "!! the AppHost exited. Last 20 log lines:" >&2
      tail -20 "$APPHOST_LOG" >&2
      die "AppHost died during startup"
    fi
    sleep 3
    waited=$((waited + 3))
    printf '    %ss — %s/%s services up, gateway %s\r' \
      "$waited" "$(printf '%s' "$found" | grep -c . || true)" "${#SERVICES[@]}" "$([ -n "$gw_port" ] && echo up || echo down)"
  done
  echo
  cat >&2 <<'EOF'
!! not every service (or the gateway) came up. The usual cause is the AppHost's silent-Waiting
   failure mode: if a secret parameter cannot resolve, its resource goes ValueMissing and every
   project that depends on it parks in Waiting with NOTHING logged. Check, in order:

     1. dotnet user-secrets --project AgendaBuddy.AppHost list
        must contain Parameters:mongodb-password, Parameters:jwt-public-key, Parameters:jwt-private-key
     2. AgendaBuddy.AppHost/Properties/launchSettings.json must exist — it sets
        DOTNET_ENVIRONMENT=Development, without which user secrets never load at all
     3. re-run with Logging__LogLevel__Aspire=Debug: resource and parameter state
        transitions are Debug-level only
     4. if MongoDB auth is broken after a password change, drop its volume:
        docker volume ls | grep mongodb-data   # then docker volume rm <name>
     5. the gateway (AppHostWiring.cs's "gateway" resource) WaitFor()s all seven services, so it
        will never report healthy before they do — if the seven are up and the gateway still isn't,
        check the AppHost log for the gateway resource specifically
EOF
  die "backend not ready after ${READY_TIMEOUT}s"
}

report_backend() {
  local dash
  echo
  say "backend is up"
  printf '    %-12s %s\n' "SERVICE" "HTTP"
  while read -r svc port; do
    [ -n "$svc" ] || continue
    printf '    %-12s http://localhost:%s\n' "$svc" "$port"
  done <<< "$PORTS"
  printf '    %-12s http://localhost:%s\n' "gateway" "$GATEWAY_PORT"
  dash="$(grep -ao 'https://localhost:[0-9]*/login?t=[0-9a-f]*' "$APPHOST_LOG" 2>/dev/null | tail -1 || true)"
  if [ -n "$dash" ]; then
    printf '    %-12s %s\n' "dashboard" "$dash"
  else
    # No log to read when --skip-apphost reused someone else's AppHost.
    printf '    %-12s %s\n' "dashboard" "see the AppHost's own console output for the login URL"
  fi
}

# ── Simulator ───────────────────────────────────────────────────────────────────────────────────
# Prefers an already-booted iPhone, then the newest iOS runtime. Emits "udid<TAB>name".
pick_device() {
  xcrun simctl list devices available -j | python3 -c '
import json, re, sys
want = sys.argv[1] if len(sys.argv) > 1 else ""
best = None
def runtime_version(rt):
    m = re.search(r"iOS-(\d+)-(\d+)", rt)
    return (int(m.group(1)), int(m.group(2))) if m else (0, 0)
for rt, devices in json.load(sys.stdin)["devices"].items():
    if "iOS" not in rt:
        continue
    for dev in devices:
        if not dev.get("isAvailable"):
            continue
        if want:
            if dev["name"] != want:
                continue
        elif "iPhone" not in dev["name"]:
            continue
        rank = (dev["state"] == "Booted", runtime_version(rt), dev["name"])
        if best is None or rank > best[0]:
            best = (rank, dev)
if best is None:
    sys.exit(1)
print(best[1]["udid"] + "\t" + best[1]["name"])
' "${1:-}"
}

# ── Run ─────────────────────────────────────────────────────────────────────────────────────────
for tool in dotnet curl lsof python3; do
  command -v "$tool" >/dev/null 2>&1 || die "$tool not found on PATH"
done

if [ "$list_devices" = "1" ]; then
  setup_xcode
  xcrun simctl list devices available
  exit 0
fi

[ "$run_app" = "1" ] && setup_xcode

if [ "$skip_apphost" = "0" ]; then
  start_apphost
else
  say "--skip-apphost: assuming the backend is already running"
fi
wait_for_services
report_backend

if [ "$run_app" = "1" ]; then
  device="$(pick_device "$device_name")" || die "no available iOS simulator${device_name:+ named \"$device_name\"} — install a simulator runtime in Xcode > Settings > Components, or list what exists with --list-devices"
  udid="${device%%$'\t'*}"
  name="${device##*$'\t'}"

  echo
  say "booting simulator: $name ($udid)"
  xcrun simctl bootstatus "$udid" -b >/dev/null
  open -a Simulator --args -CurrentDeviceUDID "$udid" || true

  # iossimulator-arm64 on Apple Silicon, -x64 on Intel. -p:MobilePlatform=ios (rather than
  # -f net10.0-ios) is what AgendaBuddy.MobileApp.csproj documents: it narrows TargetFrameworks to the iOS TFM
  # without cascading a TargetFramework override onto the Library project reference.
  case "$(uname -m)" in
    arm64) rid="iossimulator-arm64" ;;
    *)     rid="iossimulator-x64" ;;
  esac

  # F-015-T12: inject the gateway's discovered address into the simulator process. `dotnet build
  # -t:Run` for an iossimulator RID does not shell out to `xcrun simctl launch` directly here — the
  # .NET iOS build tooling does that several process-levels down — so there is no simctl invocation
  # in this script to attach a flag to. Since Xcode 11, simctl (and everything under it) reads any
  # environment variable prefixed SIMCTL_CHILD_ off the *invoking* process's environment, strips the
  # prefix, and sets it in the launched app's environment — this is the documented mechanism for
  # exactly this case (a build/launch chain you don't control the innermost step of). Exporting it
  # here means it is present in the environment of every process `dotnet build -t:Run` spawns,
  # including whichever one eventually calls simctl launch.
  gateway_url="http://localhost:${GATEWAY_PORT}/"
  say "injecting MAUI_API_BASE_URL=$gateway_url into the simulator launch (via SIMCTL_CHILD_MAUI_API_BASE_URL)"
  export SIMCTL_CHILD_MAUI_API_BASE_URL="$gateway_url"

  say "building and launching AgendaBuddy.MobileApp on the simulator ($rid) — a cold build takes a few minutes"
  dotnet build "$REPO_ROOT/AgendaBuddy.MobileApp/AgendaBuddy.MobileApp.csproj" \
    -p:MobilePlatform=ios \
    -p:RuntimeIdentifier="$rid" \
    -t:Run \
    -p:_DeviceName=":v2:udid=$udid"

  cat <<EOF

==> NOTE — the app now points at the gateway ($gateway_url), but most routes still 404.
    F-015-T12 fixed the base address: AgendaBuddy.MobileApp/Infrastructure/ApiBaseUrlResolver.cs reads
    MAUI_API_BASE_URL (set above) ahead of the ApiBaseUrl config key and the hardcoded
    http://localhost:6036/ fallback (which addressed nothing under the AppHost — ports are dynamic
    by design, AC-1.4). Still outstanding: every mobile path is missing the api/v1/ prefix the
    backend actually serves (F-015-T07, Planned). Until that lands, the ViewModels still fall back
    to AgendaBuddy.MobileApp/Services/SeedDataProvider.cs for most calls.
    Details: docs/pdlc/context/01-api-surface.md, docs/pdlc/context/16-mobile-client.md
EOF
fi

if [ "$started_apphost" = "1" ]; then
  echo
  say "backend running — Ctrl-C to shut it down. Log: $APPHOST_LOG"
  wait "$apphost_pid"
else
  echo
  say "done. The AppHost was not started by this script, so it is left running."
fi
