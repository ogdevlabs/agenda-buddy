#!/bin/bash
set -euo pipefail

# MONGO_HOST can be overridden via env (e.g. "localhost:27017" for host-network mode)
MONGO_HOST="${MONGO_HOST:-mongo:27017}"

echo "==> Waiting for MongoDB at $MONGO_HOST..."
until mongosh --host "$MONGO_HOST" --eval "db.adminCommand('ping')" --quiet 2>/dev/null; do
  sleep 1
done

echo "==> Importing providers into ProviderDb.providers..."
mongoimport --host "$MONGO_HOST" \
  --db ProviderDb \
  --collection providers \
  --jsonArray \
  --drop \
  --file /seed/seed-providers.json

echo "==> Importing customers into CustomerDb.customers..."
mongoimport --host "$MONGO_HOST" \
  --db CustomerDb \
  --collection customers \
  --jsonArray \
  --drop \
  --file /seed/seed-customers.json

echo "==> Importing credentials into IdentityDb.credentials..."
mongoimport --host "$MONGO_HOST" \
  --db IdentityDb \
  --collection credentials \
  --jsonArray \
  --drop \
  --file /seed/seed-credentials.json

echo "==> Creating unique index on credentials.email..."
mongosh --host "$MONGO_HOST" --eval '
  db = db.getSiblingDB("IdentityDb");
  db.credentials.createIndex({ email: 1 }, { unique: true });
  print("Index created.");
'

echo "==> Seed complete! Accounts available:"
echo "    Providers: sarah.mitchell@agendabuddy.dev, james.okafor@agendabuddy.dev, maria.gonzalez@agendabuddy.dev"
echo "    Customers: alex.chen@agendabuddy.dev, priya.sharma@agendabuddy.dev, david.thompson@agendabuddy.dev"
echo "    Password:  DevPass123!"
