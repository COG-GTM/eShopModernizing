#!/usr/bin/env bash
# Optional SQL mode: SQL Server 2022 in Docker. Each legacy app gets its OWN database (same EF6 seed),
# so the two EF6 initializers never race on one catalog.
# Usage: sql.sh up | down | reset-db [all|mvc|webforms] | conn <mvc|webforms>
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
SQL_CONTAINER="${SQL_CONTAINER:-eshop-legacy-sql}"
SQL_PORT="${SQL_PORT:-14330}"
SQL_SA_PASSWORD="${SQL_SA_PASSWORD:-Legacy_Harness_2022!}"   # local throwaway container only
SQLCMD=(docker exec "$SQL_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SQL_SA_PASSWORD" -b)
db_name() { echo "eShopLegacyCatalog_$1"; }
cmd="${1:-}"; shift || true
case "$cmd" in
  up)
    docker inspect "$SQL_CONTAINER" >/dev/null 2>&1 || docker run -d --name "$SQL_CONTAINER" -e ACCEPT_EULA=Y \
      -e "MSSQL_SA_PASSWORD=$SQL_SA_PASSWORD" -p "127.0.0.1:$SQL_PORT:1433" mcr.microsoft.com/mssql/server:2022-latest >/dev/null
    docker start "$SQL_CONTAINER" >/dev/null
    for _ in $(seq 1 90); do "${SQLCMD[@]}" -Q "SELECT 1" >/dev/null 2>&1 && { echo "sql up on 127.0.0.1:$SQL_PORT"; exit 0; }; sleep 2; done
    echo "sql server did not become ready"; docker logs --tail 30 "$SQL_CONTAINER"; exit 1;;
  down) docker rm -f "$SQL_CONTAINER" >/dev/null 2>&1 || true;;
  reset-db)
    which="${1:-all}"; apps=(mvc webforms); [ "$which" != all ] && apps=("$which")
    for a in "${apps[@]}"; do
      db="$(db_name "$a")"
      "${SQLCMD[@]}" -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" >/dev/null
    done;;
  conn) echo "Data Source=127.0.0.1,$SQL_PORT;Initial Catalog=$(db_name "$1");User ID=sa;Password=$SQL_SA_PASSWORD;Encrypt=False;MultipleActiveResultSets=False;";;
  *) echo "usage: $0 up|down|reset-db [all|mvc|webforms]|conn <app>"; exit 2;;
esac
