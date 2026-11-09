#!/usr/bin/env bash
# Container stack control for the .NET 8 storefront.
#   stack.sh <mock|sql> up [--no-build]   build images and start, wait until every service is healthy
#   stack.sh <mock|sql> down              stop and remove the stack
#   stack.sh <mock|sql> reset <mvc|webforms|all>
#                                         fresh app state: mock = recreate the container (new in-memory catalog);
#                                         sql = stop the app, DROP its database, recreate the container (EF Core re-creates + seeds)
#   stack.sh sql seed                     row counts (Catalog/CatalogBrand/CatalogType) + HiLo sequences per app database
#   stack.sh <mock|sql> url <mvc|webforms>
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODE="${1:?usage: stack.sh <mock|sql> <up|down|reset|seed|url> [args]}"
CMD="${2:?usage: stack.sh <mock|sql> <up|down|reset|seed|url> [args]}"
shift 2
# The layouts print Environment.MachineName (the container hostname) in the footer; transcript replay normalises only the
# replaying host's name, so the containers take that name (a recreated container would otherwise get a new random id).
export STOREFRONT_HOSTNAME="${STOREFRONT_HOSTNAME:-$(hostname)}"

case "$MODE" in
  mock) COMPOSE=(docker compose -f "$HERE/compose.mock.yml") ;;
  sql)
    ENV_FILE="$HERE/.sql.env"
    if [[ -n "${MSSQL_SA_PASSWORD:-}" ]]; then
      export MSSQL_SA_PASSWORD
    else
      if [[ ! -f "$ENV_FILE" ]]; then
        # Throwaway local SA password (upper + lower + digits), never committed (.gitignore).
        (umask 077; echo "MSSQL_SA_PASSWORD=Ss1$(openssl rand -hex 12)" > "$ENV_FILE")
      fi
      set -a; . "$ENV_FILE"; set +a
    fi
    COMPOSE=(docker compose -f "$HERE/compose.sql.yml")
    ;;
  *) echo "unknown mode: $MODE (mock|sql)" >&2; exit 2 ;;
esac

db_name() { echo "eShopStorefrontCatalog_$1"; }

sqlcmd() {  # runs inside the SQL container so the SA password never appears on the host command line
  "${COMPOSE[@]}" exec -T sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b "$@"' sqlcmd "$@"
}

apps() { if [[ "${1:-all}" == all ]]; then echo mvc webforms; else echo "$1"; fi; }

case "$CMD" in
  up)
    build=(--build); [[ "${1:-}" == --no-build ]] && build=(--no-build)
    "${COMPOSE[@]}" up -d "${build[@]}" --wait
    ;;
  down)
    "${COMPOSE[@]}" down -v --remove-orphans
    ;;
  reset)
    for app in $(apps "${1:-all}"); do
      if [[ "$MODE" == sql ]]; then
        "${COMPOSE[@]}" stop "$app" >/dev/null
        db="$(db_name "$app")"
        sqlcmd -Q "IF DB_ID(N'$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" >/dev/null
      fi
      "${COMPOSE[@]}" up -d --no-build --no-deps --force-recreate --wait "$app" >/dev/null 2>&1 \
        || { "${COMPOSE[@]}" logs --tail 50 "$app" >&2; exit 1; }
    done
    ;;
  seed)
    [[ "$MODE" == sql ]] || { echo "seed: sql mode only" >&2; exit 2; }
    echo "database,items,brands,types,hilo_sequences"
    rc=0
    for app in mvc webforms; do
      sqlcmd -d "$(db_name "$app")" -h -1 -W -s , -Q "SET NOCOUNT ON; SELECT DB_NAME(), (SELECT COUNT(*) FROM dbo.Catalog), (SELECT COUNT(*) FROM dbo.CatalogBrand), (SELECT COUNT(*) FROM dbo.CatalogType), (SELECT COUNT(*) FROM sys.sequences WHERE name IN ('catalog_hilo','catalog_brand_hilo','catalog_type_hilo'))" || rc=1
    done
    exit "$rc"
    ;;
  url)
    app="${1:?url <mvc|webforms>}"
    case "$MODE:$app" in
      mock:mvc) echo "http://127.0.0.1:${MVC_PORT:-5200}" ;;
      mock:webforms) echo "http://127.0.0.1:${WEBFORMS_PORT:-5201}" ;;
      sql:mvc) echo "http://127.0.0.1:${MVC_SQL_PORT:-5210}" ;;
      sql:webforms) echo "http://127.0.0.1:${WEBFORMS_SQL_PORT:-5211}" ;;
      *) echo "unknown app: $app" >&2; exit 2 ;;
    esac
    ;;
  *) echo "unknown command: $CMD" >&2; exit 2 ;;
esac
