#!/bin/bash
set -euo pipefail

# Agenda Buddy — Development Environment Setup
# Seeds MongoDB with providers, customers, and credentials
# so the mobile app shows populated data immediately.
#
# Prerequisites: Docker (with Compose v2)
# Usage: ./setup-dev-environment.sh [--reset]
#
# If MongoDB is already running on localhost:27017, seeds directly into it.
# Otherwise, starts a new MongoDB container on port 27017.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="$SCRIPT_DIR/docker-compose.seed.yml"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log()  { echo -e "${GREEN}[seed]${NC} $*"; }
warn() { echo -e "${YELLOW}[seed]${NC} $*"; }
err()  { echo -e "${RED}[seed]${NC} $*" >&2; }

MONGO_HOST="localhost"
MONGO_PORT="27017"
STARTED_MONGO=false

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

# Handle --reset flag: tear down existing seed containers/volumes
if [[ "${1:-}" == "--reset" ]]; then
  warn "Resetting: removing seed containers and volumes..."
  docker compose -f "$COMPOSE_FILE" down -v 2>/dev/null || true
fi

# Detect if MongoDB is already running on port 27017
mongo_reachable() {
  docker run --rm --network host mongo:7 mongosh --host "$MONGO_HOST" --port "$MONGO_PORT" --eval "db.adminCommand('ping')" --quiet 2>/dev/null
}

if mongo_reachable; then
  log "Detected existing MongoDB on $MONGO_HOST:$MONGO_PORT — seeding directly."
else
  log "No MongoDB detected on port $MONGO_PORT — starting one via Docker Compose..."
  docker compose -f "$COMPOSE_FILE" up -d mongo
  STARTED_MONGO=true

  log "Waiting for MongoDB to be ready..."
  elapsed=0
  while [ $elapsed -lt 30 ]; do
    if docker compose -f "$COMPOSE_FILE" exec -T mongo mongosh --eval "db.adminCommand('ping')" --quiet 2>/dev/null; then
      break
    fi
    sleep 1
    elapsed=$((elapsed + 1))
  done

  if [ $elapsed -eq 30 ]; then
    err "MongoDB failed to start within 30 seconds."
    exit 1
  fi
fi

log "Running seed imports..."

# Determine the MongoDB URI for the seeder
if [ "$STARTED_MONGO" = true ]; then
  # Seed via the compose network (container-to-container)
  docker compose -f "$COMPOSE_FILE" run --rm mongo-seed
else
  # Seed into the already-running MongoDB using host network
  docker run --rm --network host \
    -v "$SCRIPT_DIR/seed-providers.json:/seed/seed-providers.json:ro" \
    -v "$SCRIPT_DIR/seed-customers.json:/seed/seed-customers.json:ro" \
    -v "$SCRIPT_DIR/seed-credentials.json:/seed/seed-credentials.json:ro" \
    -v "$SCRIPT_DIR/seed-mongo.sh:/seed/seed-mongo.sh:ro" \
    -e MONGO_HOST="$MONGO_HOST:$MONGO_PORT" \
    mongo:7 bash /seed/seed-mongo.sh
fi

log ""
log "============================================="
log "  Development environment ready!"
log "============================================="
log ""
log "  MongoDB:  $MONGO_HOST:$MONGO_PORT"
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
if [ "$STARTED_MONGO" = true ]; then
  log "  To stop:  docker compose -f $COMPOSE_FILE down"
  log "  To nuke:  docker compose -f $COMPOSE_FILE down -v"
else
  log "  (Using existing MongoDB — no containers to stop)"
fi
log "  To reset: ./setup-dev-environment.sh --reset"
log ""
