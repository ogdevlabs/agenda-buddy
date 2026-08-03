#!/bin/bash
set -euo pipefail

# Agenda Buddy — Development Environment Setup
# Seeds MongoDB with providers, customers, and credentials
# so the mobile app shows populated data immediately.
#
# Prerequisites: Docker (with Compose v2)
# Usage: ./setup-dev-environment.sh [--reset]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.seed.yml"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${GREEN}[seed]${NC} $*"; }
warn() { echo -e "${YELLOW}[seed]${NC} $*"; }
err()  { echo -e "${RED}[seed]${NC} $*" >&2; }

# Check Docker is available
if ! command -v docker &>/dev/null; then
  err "Docker is not installed or not in PATH."
  err "Install from https://docs.docker.com/get-docker/"
  exit 1
fi

if ! docker info &>/dev/null; then
  err "Docker daemon is not running. Start Docker Desktop or the Docker service."
  exit 1
fi

# Handle --reset flag: tear down existing volumes
if [[ "${1:-}" == "--reset" ]]; then
  warn "Resetting: removing existing containers and volumes..."
  docker compose -f "$COMPOSE_FILE" down -v 2>/dev/null || true
fi

log "Starting MongoDB and running seed imports..."
docker compose -f "$COMPOSE_FILE" up -d mongo

log "Waiting for MongoDB healthcheck..."
timeout=30
while [ $timeout -gt 0 ]; do
  if docker compose -f "$COMPOSE_FILE" exec -T mongo mongosh --eval "db.adminCommand('ping')" --quiet 2>/dev/null; then
    break
  fi
  sleep 1
  timeout=$((timeout - 1))
done

if [ $timeout -eq 0 ]; then
  err "MongoDB failed to start within 30 seconds."
  exit 1
fi

log "Running seed import container..."
docker compose -f "$COMPOSE_FILE" run --rm mongo-seed

log ""
log "============================================="
log "  Development environment ready!"
log "============================================="
log ""
log "  MongoDB:  localhost:27017"
log ""
log "  Databases seeded:"
log "    - ProviderDb.providers  (3 providers with services)"
log "    - CustomerDb.customers  (3 customers)"
log "    - IdentityDb.credentials (6 login credentials)"
log ""
log "  Test Accounts (password: DevPass123!):"
log "    Providers:"
log "      sarah.mitchell@agendabuddy.dev  (Fitness Coach)"
log "      james.okafor@agendabuddy.dev    (Software Instructor)"
log "      maria.gonzalez@agendabuddy.dev  (Therapist)"
log "    Customers:"
log "      alex.chen@agendabuddy.dev"
log "      priya.sharma@agendabuddy.dev"
log "      david.thompson@agendabuddy.dev"
log ""
log "  To reset: ./setup-dev-environment.sh --reset"
log "  To stop:  docker compose -f $COMPOSE_FILE down"
log "  To nuke:  docker compose -f $COMPOSE_FILE down -v"
log ""
