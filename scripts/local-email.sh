#!/usr/bin/env bash
# Read the latest locally captured Identity email and optionally open its link in a booted iOS simulator.
#
#   ./scripts/local-email.sh customer@local.test
#   GATEWAY_URL=http://localhost:58833 ./scripts/local-email.sh customer@local.test --open-ios

set -euo pipefail

gateway_url="${GATEWAY_URL:-http://localhost:6080}"
email="${1:-}"
action="${2:-}"

if [[ -z "$email" ]]; then
  echo "usage: GATEWAY_URL=http://localhost:<port> $0 <email> [--open-ios]" >&2
  exit 2
fi

for tool in curl jq; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: $tool not found on PATH" >&2; exit 1; }
done

message=$(curl -fsS --get --data-urlencode "to=$email" \
  "${gateway_url%/}/api/v1/auth/dev/emails/latest")

printf '%s' "$message" | jq '{toAddress, subject, capturedAt, text}'

if [[ "$action" != "--open-ios" ]]; then
  exit 0
fi

command -v xcrun >/dev/null 2>&1 || { echo "error: xcrun not found on PATH" >&2; exit 1; }

link=$(printf '%s' "$message" | jq -r '.text' | grep -o 'agendame://[^[:space:]]*' | head -1)
[[ -n "$link" ]] || { echo "error: captured email has no local AgendaMe link" >&2; exit 1; }

udid=$(xcrun simctl list devices booted -j | python3 -c '
import json, sys
for devices in json.load(sys.stdin)["devices"].values():
    for device in devices:
        if device.get("state") == "Booted" and "iPhone" in device.get("name", ""):
            print(device["udid"])
            raise SystemExit(0)
raise SystemExit(1)
')

xcrun simctl openurl "$udid" "$link"
echo "Opened confirmation link on iOS simulator $udid"