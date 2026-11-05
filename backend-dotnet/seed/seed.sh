#!/bin/sh
# Loads every data/museums/*.json into the dev Postgres (container artaround-dev-db).
set -e
cd "$(dirname "$0")/../.."
for file in data/museums/*.json; do
    echo "Seeding $file"
    docker exec -i artaround-dev-db psql -q -U artaround -d artaround -v json="$(cat "$file")" \
        < backend-dotnet/seed/seed-museums.sql
done
