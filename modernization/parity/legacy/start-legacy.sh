#!/usr/bin/env bash
# (Re)starts one legacy app under xsp4 from the throwaway copies made by setup-legacy.sh and waits
# until it answers (first Razor/ASPX compile takes 10-60 s). Restarting = fresh in-memory mock data.
#
#   start-legacy.sh mvc|webforms|mvc-sql|wf-sql      (re)start in background, wait for HTTP 200 on a non-catalog probe URL
#   start-legacy.sh stop mvc|webforms|mvc-sql|wf-sql
#   LEGACY_RUN_DIR (default ~/legacy-run); MVC_PORT=5100 WEBFORMS_PORT=5101 MVC_SQL_PORT=5110 WF_SQL_PORT=5111
#   mvc-sql|wf-sql: LEGACY_SQL_RESET=1 drops the catalog DB first so CatalogDBInitializer re-creates + re-seeds it.
set -euo pipefail
RUN="${LEGACY_RUN_DIR:-$HOME/legacy-run}"
ACTION=start
if [ "${1:-}" = stop ]; then ACTION=stop; shift; fi
APP="${1:?usage: start-legacy.sh [stop] mvc|webforms|mvc-sql|wf-sql}"
case "$APP" in
  mvc) DIR="$RUN/eShopLegacyMVCSolution/src/eShopLegacyMVC"; PORT="${MVC_PORT:-5100}";;
  webforms) DIR="$RUN/eShopLegacyWebFormsSolution/src/eShopLegacyWebForms"; PORT="${WEBFORMS_PORT:-5101}";;
  mvc-sql) DIR="$RUN/mvc-sql"; PORT="${MVC_SQL_PORT:-5110}";;
  wf-sql) DIR="$RUN/wf-sql"; PORT="${WF_SQL_PORT:-5111}";;
  *) echo "unknown app $APP" >&2; exit 2;;
esac
[ -d "$DIR" ] || { echo "$DIR missing - run setup-legacy.sh first" >&2; exit 1; }

fuser -k -TERM -n tcp "$PORT" >/dev/null 2>&1 || true
for _ in $(seq 1 50); do fuser -n tcp "$PORT" >/dev/null 2>&1 || break; sleep 0.2; done
fuser -k -KILL -n tcp "$PORT" >/dev/null 2>&1 || true
SQL_APP=0; case "$APP" in mvc-sql|wf-sql) SQL_APP=1;; esac
if [ "$SQL_APP" = 1 ] && [ -f "$DIR/Web.config" ]; then
  # Scrub the injected password whenever the SQL copy is (re)started or stopped; it is re-injected below on start.
  sed -i -E 's#Password=[^;"]*;#Password=__SET_BY_START_LEGACY__;#' "$DIR/Web.config"
fi
[ "$ACTION" = stop ] && { echo "$APP stopped"; exit 0; }

if [ "$SQL_APP" = 1 ]; then
  : "${LEGACY_SQL_PASSWORD:?set LEGACY_SQL_PASSWORD to the local SQL Server sa password}"
  # The throwaway Web.config is written without a password by setup-legacy.sh; inject it here (never committed).
  PW_ESC=$(printf '%s' "$LEGACY_SQL_PASSWORD" | sed 's/[&#\\]/\\&/g')
  chmod 600 "$DIR/Web.config"
  sed -i -E "s#Password=[^;\"]*;#Password=$PW_ESC;#" "$DIR/Web.config"
fi
if [ "$SQL_APP" = 1 ] && [ "${LEGACY_SQL_RESET:-0}" = 1 ]; then
  # mvc-sql and wf-sql share the catalog DB and cache HiLo blocks in memory: stop the peer before the drop so it
  # cannot hand out ids that collide with the re-seeded sequence (restart it afterwards if it is needed)
  if [ "$APP" = mvc-sql ]; then PEER=wf-sql; PEER_PORT="${WF_SQL_PORT:-5111}"; else PEER=mvc-sql; PEER_PORT="${MVC_SQL_PORT:-5110}"; fi
  if fuser -n tcp "$PEER_PORT" >/dev/null 2>&1; then
    "${BASH_SOURCE[0]}" stop "$PEER" >/dev/null
    echo "$PEER stopped (shared catalog DB is being reset)"
  fi
  /opt/mssql-tools18/bin/sqlcmd -S "${LEGACY_SQL_SERVER:-127.0.0.1,1433}" -U sa -P "$LEGACY_SQL_PASSWORD" -C -b -Q \
    "IF DB_ID('Microsoft.eShopOnContainers.Services.CatalogDb') IS NOT NULL BEGIN ALTER DATABASE [Microsoft.eShopOnContainers.Services.CatalogDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [Microsoft.eShopOnContainers.Services.CatalogDb]; END" >/dev/null
fi

LOG="$RUN/xsp-$APP.log"
cd "$DIR"
setsid nohup xsp4 --port "$PORT" --address 127.0.0.1 --root . --nonstop > "$LOG" 2>&1 < /dev/null &
case "$APP" in webforms|wf-sql) PROBE=/About.aspx;; *) PROBE=/api/brands;; esac
# readiness probe deliberately avoids the catalog list: listing populates mock navigation properties (INVENTORY quirk 4)
for i in $(seq 1 120); do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 120 "http://127.0.0.1:$PORT$PROBE" || true)
  if [ "$code" = 200 ]; then echo "$APP up on http://127.0.0.1:$PORT (after ${i} tries)"; exit 0; fi
  sleep 1
done
echo "$APP did not answer 200 on port $PORT; see $LOG" >&2; exit 1
