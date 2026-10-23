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
#   NET8_SQL_DB overrides the catalog DB name (e.g. to sit next to the legacy DB in one server); it is dropped first (NET8_SQL_RESET=0 keeps it) so the app re-creates and re-seeds it.
#   Both apps share that DB, so a SQL-mode peer app that is running is stopped before the drop and restarted
#   (without another reset) once this app has re-seeded; otherwise its cached HiLo block would collide.
# --docker: restart the eShopNet8/docker-compose.yml service instead (profile mock: mvc/webforms on 5200/5201;
#   --sql: profile sql, mvc-sql/webforms-sql on 5210/5211, DB dropped via the sqldata container first).
#   Needs the stack to have been created once with `docker compose ... up -d --build`.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
SOL="$REPO/eShopNet8"
COMPOSE="$SOL/docker-compose.yml"
DB="${NET8_SQL_DB:-Microsoft.eShopOnContainers.Services.CatalogDb}"
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
if [ "$APP" = mvc ]; then PEER=webforms; else PEER=mvc; fi
RESET=0; [ "$SQL" = 1 ] && [ "$ACTION" = start ] && [ "${NET8_SQL_RESET:-1}" = 1 ] && RESET=1
if [ "$SQL" = 1 ] && [ "$DOCKER" = 0 ] && [ "$ACTION" = start ]; then
  : "${NET8_SQL_PASSWORD:?set NET8_SQL_PASSWORD to the local SQL Server sa password}"
fi

# a SQL peer stopped for the shared-DB reset is brought back even if this restart fails
RESTORE_PEER=""
restore_peer() {
  local rc=$?
  if [ "$rc" != 0 ] && [ -n "$RESTORE_PEER" ]; then
    echo "restart of $APP failed; restoring $PEER" >&2
    eval "$RESTORE_PEER" || true
  fi
  exit "$rc"
}
trap restore_peer EXIT

wait_healthy() { # port logfile [name]
  local name="${3:-$APP}"
  for i in $(seq 1 180); do
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$1$PROBE" || true)
    if [ "$code" = 200 ]; then echo "$name up on http://127.0.0.1:$1 ($PROBE after ${i} tries)"; return 0; fi
    sleep 1
  done
  echo "$name did not answer 200 on http://127.0.0.1:$1$PROBE; see $2" >&2
  return 1
}

if [ "$DOCKER" = 1 ]; then
  if [ -n "${NET8_SQL_DB:-}" ] && [ "$NET8_SQL_DB" != Microsoft.eShopOnContainers.Services.CatalogDb ]; then
    # the compose connection strings name the default DB; resetting another one would leave the app on stale data
    echo "NET8_SQL_DB is not supported with --docker (compose uses Microsoft.eShopOnContainers.Services.CatalogDb)" >&2; exit 2
  fi
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
  if [ "$RESET" = 1 ]; then
    PEER_SERVICE="$PEER-sql"
    if [ "$PEER" = mvc ]; then PEER_PORT="${NET8_SQL_MVC_PORT:-5210}"; else PEER_PORT="${NET8_SQL_WEBFORMS_PORT:-5211}"; fi
    PEER_UP=0; dc ps --status running --services 2>/dev/null | grep -qx "$PEER_SERVICE" && PEER_UP=1
    # NET8_DOCKER_SOLO=1: run only this service (no compose depends_on, peer left stopped), like one legacy app per DB
    [ "${NET8_DOCKER_SOLO:-0}" = 1 ] && PEER_UP=0
    quiet dc stop "$SERVICE" "$PEER_SERVICE"
    [ "$PEER_UP" = 1 ] && RESTORE_PEER='quiet dc start "$PEER_SERVICE"'
    # the password is read inside the container from its own environment, never passed on a host command line
    dc exec -T sqldata bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -Q "$0" >/dev/null' "$DROP_DB"
    # mvc-sql seeds first when both run (same order as the compose depends_on)
    if [ "$PEER_UP" = 1 ] && [ "$PEER" = mvc ]; then
      quiet dc start "$PEER_SERVICE"; wait_healthy "$PEER_PORT" "docker compose -f $COMPOSE --profile $PROFILE logs $PEER_SERVICE" "$PEER_SERVICE"
    fi
    if [ "${NET8_DOCKER_SOLO:-0}" = 1 ]; then quiet dc up -d --no-deps "$SERVICE"; else quiet dc start "$SERVICE"; fi
    wait_healthy "$PORT" "docker compose -f $COMPOSE --profile $PROFILE logs $SERVICE"
    RESTORE_PEER=""
    if [ "$PEER_UP" = 1 ] && [ "$PEER" = webforms ]; then
      quiet dc start "$PEER_SERVICE"; wait_healthy "$PEER_PORT" "docker compose -f $COMPOSE --profile $PROFILE logs $PEER_SERVICE" "$PEER_SERVICE"
    fi
    exit 0
  fi
  quiet dc restart "$SERVICE"
  wait_healthy "$PORT" "docker compose -f $COMPOSE --profile $PROFILE logs $SERVICE"
  exit $?
fi

RUN="${NET8_RUN_DIR:-${XDG_CACHE_HOME:-$HOME/.cache}/eshopnet8-run}"
OUT="$RUN/$APP"
LOG="$RUN/$APP.log"; [ "$SQL" = 1 ] && LOG="$RUN/$APP-sql.log"
port_of() { if [ "$1" = mvc ]; then echo "${NET8_MVC_PORT:-5200}"; else echo "${NET8_WEBFORMS_PORT:-5201}"; fi; }
PORT="$(port_of "$APP")"
mkdir -p "$RUN"

kill_port() {
  fuser -k -TERM -n tcp "$1" >/dev/null 2>&1 || true
  for _ in $(seq 1 50); do fuser -n tcp "$1" >/dev/null 2>&1 || break; sleep 0.2; done
  fuser -k -KILL -n tcp "$1" >/dev/null 2>&1 || true
}
kill_port "$PORT"
rm -f "$RUN/$APP.sql-mode"
[ "$ACTION" = stop ] && { echo "$APP stopped"; exit 0; }

# a peer started by this script in SQL mode shares the DB about to be dropped
PEER_SQL=0
if [ "$RESET" = 1 ] && [ -f "$RUN/$PEER.sql-mode" ] && fuser -n tcp "$(port_of "$PEER")" >/dev/null 2>&1; then
  PEER_SQL=1; kill_port "$(port_of "$PEER")"; rm -f "$RUN/$PEER.sql-mode"
  RESTORE_PEER='NET8_SQL_RESET=0 "$HERE/start-net8.sh" "$PEER" --sql'
fi

STAMP="$OUT/.published"
if [ "${NET8_REBUILD:-0}" = 1 ] || [ ! -f "$STAMP" ] || \
   [ -n "$(find "$SOL/src" "$SOL/global.json" \( -name bin -o -name obj \) -prune -o -newer "$STAMP" -print -quit)" ]; then
  echo "publishing $PROJECT -> $OUT"
  rm -rf "$OUT"
  dotnet publish "$SOL/src/$PROJECT/$PROJECT.csproj" -c Release -o "$OUT" --nologo -v quiet >"$RUN/$APP-publish.log" 2>&1 \
    || { cat "$RUN/$APP-publish.log" >&2; exit 1; }
  touch "$STAMP"
fi

ENV=(ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}")
if [ "$SQL" = 1 ]; then
  SERVER="${NET8_SQL_SERVER:-127.0.0.1,1433}"
  if [ "$RESET" = 1 ]; then
    SQLCMD="$(command -v sqlcmd || echo /opt/mssql-tools18/bin/sqlcmd)"
    SQLCMDPASSWORD="$NET8_SQL_PASSWORD" "$SQLCMD" -S "$SERVER" -U sa -C -b -Q "$DROP_DB" >/dev/null
  fi
  ENV+=(Catalog__UseMockData=false
        "ConnectionStrings__CatalogDBContext=Server=$SERVER;Initial Catalog=$DB;User Id=sa;Password=$NET8_SQL_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True")
else
  ENV+=(Catalog__UseMockData=true)
fi

# exported by the shell builtin in a subshell (never on an exec'd command line) so the password is not in ps
( cd "$OUT"   # content root = publish dir (wwwroot, Pics, appsettings.json)
  export "${ENV[@]}"
  exec setsid nohup dotnet "$PROJECT.dll" --urls "http://127.0.0.1:$PORT" >"$LOG" 2>&1 </dev/null ) &
[ "$SQL" = 1 ] && touch "$RUN/$APP.sql-mode"
wait_healthy "$PORT" "$LOG"
if [ "$PEER_SQL" = 1 ]; then RESTORE_PEER=""; NET8_SQL_RESET=0 "$HERE/start-net8.sh" "$PEER" --sql; fi
