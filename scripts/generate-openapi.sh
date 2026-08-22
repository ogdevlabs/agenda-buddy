#!/usr/bin/env bash
# generate-openapi.sh — regenerate the OpenAPI spec for every service, on demand.
#
#   ./scripts/generate-openapi.sh                # all seven services
#   ./scripts/generate-openapi.sh Provider       # just one
#   ./scripts/generate-openapi.sh Provider Customer
#
# Output: docs/api/openapi/<service>.json  (+ index.md summarising the routes)
#
# ─────────────────────────────────────────────────────────────────────────────────────────────────
# WHY THIS RUNS EACH SERVICE STANDALONE, AND NOT UNDER THE APPHOST
#
# Swashbuckle is registered inside `if (app.Environment.IsDevelopment())` in all seven services, so
# `/swagger/v1/swagger.json` only exists in Development. Under `dotnet run --project
# AgendaBuddy.AppHost` the services do NOT run as Development — verified: every service answered
# /alive and /health, and every /swagger/v1/swagger.json returned 404. So the AppHost is the wrong
# host for spec generation. Each service is started here directly, as Development, on a scratch port.
#
# WHY A THROWAWAY MONGO CONTAINER
#
# Two services do real work at startup: `AddAgendaBuddyAuthentication()` throws
# ApplicationException if JWT_PUBLIC_KEY is missing (Library.ServerAuth/AuthenticationExtensions.cs),
# and Profession runs ProfessionSeedHostedService, which connects to MongoDB in StartAsync — an
# unhandled exception there stops the host before Kestrel serves anything. So this script provides a
# real, empty, disposable Mongo and a throwaway RSA keypair. Nothing here touches a real database and
# no key is written to the repo.
#
# Requires: dotnet, docker (Rancher Desktop: docker lives in ~/.rd/bin, added to PATH below),
#           openssl, curl, python3.
# ─────────────────────────────────────────────────────────────────────────────────────────────────

set -euo pipefail

# Rancher Desktop puts docker outside the default PATH. Harmless if it is already there.
export PATH="$HOME/.rd/bin:$PATH"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="$REPO_ROOT/docs/api/openapi"
MONGO_IMAGE="mongo:7.0"
MONGO_NAME="agenda-buddy-openapi-mongo"
WORK="$(mktemp -d)"

ALL_SERVICES=(Provider Services Calendar Booking Customer Profession Identity)
if [ "$#" -gt 0 ]; then SERVICES=("$@"); else SERVICES=("${ALL_SERVICES[@]}"); fi

svc_pid=""

cleanup() {
  local code=$?
  [ -n "$svc_pid" ] && kill "$svc_pid" 2>/dev/null || true
  docker rm -f "$MONGO_NAME" >/dev/null 2>&1 || true
  rm -rf "$WORK"
  exit $code
}
trap cleanup EXIT INT TERM

die() { echo "error: $*" >&2; exit 1; }

for tool in dotnet docker openssl curl python3; do
  command -v "$tool" >/dev/null 2>&1 || die "$tool not found on PATH"
done

mkdir -p "$OUT_DIR"

# ── Throwaway RSA keypair, in the temp dir only. Never written into the repo: the AC-3 hygiene test
#    (Library.Tests/Security/KeyMaterialHygieneTest) fails the build if PEM material is ever tracked.
echo "==> generating a throwaway RSA keypair (temp dir only, never committed)"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$WORK/private.pem" 2>/dev/null
openssl rsa -in "$WORK/private.pem" -pubout -out "$WORK/public.pem" 2>/dev/null
export JWT_PUBLIC_KEY="$(cat "$WORK/public.pem")"
export JWT_PRIVATE_KEY="$(cat "$WORK/private.pem")"

# ── Disposable Mongo, so Profession's seed hosted service can start.
echo "==> starting a disposable MongoDB ($MONGO_IMAGE)"
docker rm -f "$MONGO_NAME" >/dev/null 2>&1 || true
docker run -d --name "$MONGO_NAME" -P "$MONGO_IMAGE" >/dev/null
mongo_port="$(docker port "$MONGO_NAME" 27017/tcp | head -1 | sed 's/.*://')"
[ -n "$mongo_port" ] || die "could not determine the Mongo container port"
export ConnectionStrings__mongodb="mongodb://127.0.0.1:${mongo_port}/?directConnection=true"
echo "    mongo on 127.0.0.1:${mongo_port}"

# Aim every service at its own scratch database so a rerun cannot inherit state.
export MongoDbSettings__DatabaseName="openapi_scratch"
export ASPNETCORE_ENVIRONMENT=Development

# The Http port a service binds standalone, from its own appsettings.json.
port_of() {
  python3 -c "
import json,sys
try:
    d = json.load(open('$REPO_ROOT/$1/appsettings.json'))
    print(d['Kestrel']['Endpoints']['Http']['Url'].rsplit(':', 1)[1])
except Exception:
    print('')"
}

generated=()
failed=()

for service in "${SERVICES[@]}"; do
  proj="$REPO_ROOT/$service/$service.csproj"
  [ -f "$proj" ] || { echo "!! no such service: $service (skipping)"; failed+=("$service"); continue; }

  # Each service pins its own Kestrel endpoints in appsettings.json, and IConfiguration WINS over
  # `--urls` ("Overriding address(es) ... Binding to endpoints defined via IConfiguration") -- so poll
  # the service's own documented port rather than a scratch one. Deterministic, and it is the same port
  # the Bruno collection targets.
  port="$(port_of "$service")"
  [ -n "$port" ] || { echo "    ✗ no Kestrel Http port in $service/appsettings.json"; failed+=("$service"); continue; }

  echo "==> $service (port $port)"
  # --no-launch-profile: launchSettings.json would otherwise override ASPNETCORE_ENVIRONMENT.
  dotnet run --project "$proj" --no-launch-profile > "$WORK/$service.log" 2>&1 &
  svc_pid=$!

  spec_url="http://127.0.0.1:${port}/swagger/v1/swagger.json"
  ready=""
  for _ in $(seq 1 60); do            # ~60s: first run includes a build
    if curl -fs --max-time 2 "$spec_url" -o "$WORK/$service.json" 2>/dev/null; then ready=1; break; fi
    kill -0 "$svc_pid" 2>/dev/null || break   # process died — stop waiting
    sleep 1
  done

  if [ -n "$ready" ]; then
    python3 -m json.tool "$WORK/$service.json" > "$OUT_DIR/$service.json"
    routes="$(python3 -c "import json;print(len(json.load(open('$OUT_DIR/$service.json')).get('paths',{})))")"
    echo "    ✓ $routes paths -> docs/api/openapi/$service.json"
    generated+=("$service")
  else
    echo "    ✗ failed — last 15 log lines:"
    tail -15 "$WORK/$service.log" | sed 's/^/      /'
    failed+=("$service")
  fi

  kill "$svc_pid" 2>/dev/null || true
  wait "$svc_pid" 2>/dev/null || true
  svc_pid=""
done

# ── A human-readable index, so the specs are browsable without a viewer.
{
  echo "# OpenAPI specs"
  echo
  echo "Generated by \`scripts/generate-openapi.sh\` on $(date -u +%Y-%m-%dT%H:%M:%SZ)."
  echo "**Regenerate any time** — this directory is a build artifact, not a hand-maintained document."
  echo
  echo "| Service | Standalone port | Spec | Paths |"
  echo "|---|---|---|---|"
  for s in "${generated[@]:-}"; do
    port="$(port_of "$s")"
    n="$(python3 -c "import json;print(len(json.load(open('$OUT_DIR/$s.json')).get('paths',{})))")"
    echo "| $s | $port | [\`$s.json\`]($s.json) | $n |"
  done
  echo
  echo "## Every route"
  echo
  for s in "${generated[@]:-}"; do
    echo "### $s"
    echo
    python3 -c "
import json
d = json.load(open('$OUT_DIR/$s.json'))
for path, ops in sorted(d.get('paths', {}).items()):
    for verb in sorted(ops):
        print(f'- \`{verb.upper():6} {path}\`')
"
    echo
  done
} > "$OUT_DIR/index.md"

echo
echo "==> generated: ${#generated[@]}/${#SERVICES[@]}  ->  docs/api/openapi/"
if [ "${#failed[@]}" -gt 0 ]; then echo "==> FAILED: ${failed[*]}"; exit 1; fi
echo "==> index: docs/api/openapi/index.md"
