#!/usr/bin/env bash
# Resets the SQL-mode stack to the seeded catalog: drops the catalog database, then restarts the given
# compose service(s) so CatalogDBInitializer recreates and reseeds it (the SQL analogue of restarting
# the mock singleton). Used as --restart-cmd-<app> for replay.py / side_by_side.py in SQL mode.
#   usage: reset-sql.sh <env-file> <service>...   (env file holds MSSQL_SA_PASSWORD, see src-net8/docker/sql.env.example)
set -euo pipefail
env_file="$1"; shift
here="$(cd "$(dirname "$0")" && pwd)"
compose=(docker compose -f "$here/../../../src-net8/docker-compose.yml" --env-file "$env_file" --profile sql)
db="Microsoft.eShopOnContainers.Services.CatalogDb"
"${compose[@]}" exec -T sqldata bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -Q "IF DB_ID('"'$db'"') IS NOT NULL BEGIN ALTER DATABASE ['"$db"'] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ['"$db"']; END"'
[ $# -gt 0 ] || exit 0
"${compose[@]}" restart "$@" >/dev/null
"${compose[@]}" up -d --wait "$@" >/dev/null
