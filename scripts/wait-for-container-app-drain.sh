#!/usr/bin/env bash

set -euo pipefail

resource_group=${1:?resource group is required}
max_wait_seconds=${2:-240}
deadline=$((SECONDS + max_wait_seconds))

mapfile -t apps < <(az containerapp list --resource-group "$resource_group" --query "[].name" -o tsv)
if [ "${#apps[@]}" -eq 0 ]; then
  echo "::error::No container apps found in $resource_group."
  exit 1
fi

while true; do
  running=0
  for app in "${apps[@]}"; do
    replicas=$(az containerapp replica list --name "$app" --resource-group "$resource_group" \
      --query "length(@)" -o tsv)
    running=$((running + replicas))
  done

  if [ "$running" -eq 0 ]; then
    echo "All ${#apps[@]} container apps have drained."
    exit 0
  fi

  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "$running replica records remain after this ${max_wait_seconds}-second drain slice."
    exit 75
  fi

  echo "Waiting for $running replica records to terminate."
  sleep 10
done
