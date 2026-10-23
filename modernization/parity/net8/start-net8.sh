#!/usr/bin/env bash
# (Re)starts one migrated .NET 8 app and returns once it answers its health endpoint: the restart command for
# modernization/parity/replay.py (restarting = fresh in-memory mock data, or a freshly seeded DB with --sql).
# The probe never renders a catalog page (CONTRACT section 7, INVENTORY Q4).
#
#   start-net8.sh [stop] mvc|webforms [--sql] [--docker]
#
# local (default): `dotnet publish` once into $NET8_RUN_DIR/<app> (re-published only when eShopNet8/src or
#   global.json changed since the last publish, or NET8_REBUILD=1), then run it on 127.0.0.1.
#   Ports: NET8_MVC_PORT=5200 NET8_WEBFORMS_PORT=5201. Probe: /health (mock) or /health/ready (--sql).
#   --sql: UseMockData=false against NET8_SQL_SERVER (default 127.0.0.1,1433) with sa / NET8_SQL_PASSWORD;
#   the catalog DB is dropped first (NET8_SQL_RESET=0 keeps it) so the app re-creates and re-seeds it.
# --docker: restart the eShopNet8/docker-compose.yml service instead (profile mock: mvc/webforms on 5200/5201;
#   --sql: profile sql, mvc-sql/webforms-sql on 5210/5211, DB dropped via the sqldata container first).
#   Needs the stack to have been created once with `docker compose ... up -d --build`.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
SOL="$REPO/eShopNet8"
COMPOSE="$SOL/docker-compose.yml"
DB=Microsoft.eShopOnContainers.Services.CatalogDb
DROP_DB="IF DB_ID('$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END"

ACTION=start
if [ "${1:-}" = stop ]; then ACTION=stop; shift; fi
APP="${1:?usage: start-net8.sh [stop] mvc|webforms [--sql] [--docker]}"; shift
SQL=0; DOCKER=0
for arg in "$@"; do
  case "$arg" in
    --sql) SQL=1;;
    --docker) DOCKER=1;;
    *) echo "unknown option $arg" >&2; exit 2;;
  esac
done
case "$APP" in
  mvc) PROJECT=eShopNet8.Mvc;;
  webforms) PROJECT=eShopNet8.WebForms;;
  *) echo "unknown app $APP (mvc|webforms)" >&2; exit 2;;
esac
PROBE=/health; [ "$SQL" = 1 ] && PROBE=/health/ready

wait_healthy() { # port logfile
  for i in $(seq 1 180); do
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$1$PROBE" || true)
    if [ "$code" = 200 ]; then echo "$APP up on http://127.0.0.1:$1 ($PROBE after ${i} tries)"; return 0; fi
    sleep 1
  done
  echo "$APP did not answer 200 on http://127.0.0.1:$1$PROBE; see $2" >&2
  return 1
}

if [ "$DOCKER" = 1 ]; then
  ENV_FILE=(); [ -f "$SOL/docker/.env" ] && ENV_FILE=(--env-file "$SOL/docker/.env")
  if [ "$SQL" = 1 ]; then
    PROFILE=sql; SERVICE="$APP-sql"
    if [ "$APP" = mvc ]; then PORT="${NET8_SQL_MVC_PORT:-5210}"; else PORT="${NET8_SQL_WEBFORMS_PORT:-5211}"; fi
  else
    PROFILE=mock; SERVICE="$APP"
    if [ "$APP" = mvc ]; then PORT="${NET8_MVC_PORT:-5200}"; else PORT="${NET8_WEBFORMS_PORT:-5201}"; fi
  fi
  dc() { docker compose -f "$COMPOSE" "${ENV_FILE[@]}" --profile "$PROFILE" "$@"; }
  quiet() { local out; out=$("$@" 2>&1) || { printf '%s\n' "$out" >&2; return 1; }; }
  if [ "$ACTION" = stop ]; then quiet dc stop "$SERVICE"; echo "$SERVICE stopped"; exit 0; fi
  if [ "$SQL" = 1 ] && [ "${NET8_SQL_RESET:-1}" = 1 ]; then
    # the password is read inside the container from its own environment, never passed on a host command line
    quiet dc stop "$SERVICE"
    dc exec -T sqldata bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -Q "$0" >/dev/null' "$DROP_DB"
    quiet dc start "$SERVICE"
  else
    quiet dc restart "$SERVICE"
  fi
  wait_healthy "$PORT" "docker compose -f $COMPOSE --profile $PROFILE logs $SERVICE"
  exit $?
fi

RUN="${NET8_RUN_DIR:-${XDG_CACHE_HOME:-$HOME/.cache}/eshopnet8-run}"
OUT="$RUN/$APP"
LOG="$RUN/$APP.log"; [ "$SQL" = 1 ] && LOG="$RUN/$APP-sql.log"
if [ "$APP" = mvc ]; then PORT="${NET8_MVC_PORT:-5200}"; else PORT="${NET8_WEBFORMS_PORT:-5201}"; fi
mkdir -p "$RUN"

fuser -k -TERM -n tcp "$PORT" >/dev/null 2>&1 || true
for _ in $(seq 1 50); do fuser -n tcp "$PORT" >/dev/null 2>&1 || break; sleep 0.2; done
fuser -k -KILL -n tcp "$PORT" >/dev/null 2>&1 || true
[ "$ACTION" = stop ] && { echo "$APP stopped"; exit 0; }

STAMP="$OUT/.published"
if [ "${NET8_REBUILD:-0}" = 1 ] || [ ! -f "$STAMP" ] || \
   [ -n "$(find "$SOL/src" "$SOL/global.json" -newer "$STAMP" -not -path '*/bin/*' -not -path '*/obj/*' -print -quit)" ]; then
  echo "publishing $PROJECT -> $OUT"
  rm -rf "$OUT"
  dotnet publish "$SOL/src/$PROJECT/$PROJECT.csproj" -c Release -o "$OUT" --nologo -v quiet >"$RUN/$APP-publish.log" 2>&1 \
    || { cat "$RUN/$APP-publish.log" >&2; exit 1; }
  touch "$STAMP"
fi

ENV=(ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}")
if [ "$SQL" = 1 ]; then
  : "${NET8_SQL_PASSWORD:?set NET8_SQL_PASSWORD to the local SQL Server sa password}"
  SERVER="${NET8_SQL_SERVER:-127.0.0.1,1433}"
  if [ "${NET8_SQL_RESET:-1}" = 1 ]; then
    SQLCMD="$(command -v sqlcmd || echo /opt/mssql-tools18/bin/sqlcmd)"
    SQLCMDPASSWORD="$NET8_SQL_PASSWORD" "$SQLCMD" -S "$SERVER" -U sa -C -b -Q "$DROP_DB" >/dev/null
  fi
  # passed through the environment (not argv) so it does not show up in ps
  ENV+=(Catalog__UseMockData=false
        "ConnectionStrings__CatalogDBContext=Server=$SERVER;Initial Catalog=$DB;User Id=sa;Password=$NET8_SQL_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True")
else
  ENV+=(Catalog__UseMockData=true)
fi

cd "$OUT"   # content root = publish dir (wwwroot, Pics, appsettings.json)
env "${ENV[@]}" setsid nohup dotnet "$PROJECT.dll" --urls "http://127.0.0.1:$PORT" >"$LOG" 2>&1 </dev/null &
wait_healthy "$PORT" "$LOG"
