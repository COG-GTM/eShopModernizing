#!/usr/bin/env bash
# Side-by-side parity run (story MBA-3849): legacy (Mono + xsp4) and modern (.NET 8) apps from the same fresh seed,
# HTTP replay of scenarios.py against both, results under $PARITY_OUT/<mode>/<app>/.
#
# Usage: modern/parity/run-parity.sh [mock|sql|all] [--keep-running] | --stop
# Prereqs: legacy scratch builds from run-legacy.sh (mock: LEGACY_RUN_DIR, sql: LEGACY_SQL_RUN_DIR),
#          for sql a SQL Server 2022 container (PARITY_MSSQL_CONTAINER, published on 127.0.0.1:1433) and
#          PARITY_SQL_PASSWORD (sa). Modern hosts are published from this checkout into $PARITY_WORK/publish.
# PARITY_NO_REPLAY=1 with --keep-running just starts the four hosts on fresh data (screenshots, browser runs).
# Ports: legacy mock 5100/5101, modern mock 5200/5201, legacy sql 5110/5111, modern sql 5210/5211.
# Every server is started detached and stopped by its PID (never pkill: an unrelated xsp runs on :8084).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODERN="$(cd "$HERE/.." && pwd)"
WORK="${PARITY_WORK:-$HOME/parity-work}"
OUT="${PARITY_OUT:-$WORK/results}"
LEGACY_MOCK="${LEGACY_RUN_DIR:-$HOME/mecca-legacy-run}/mock"
LEGACY_SQL="${LEGACY_SQL_RUN_DIR:-$HOME/mecca-legacy-run-sql}/sql"
CONTAINER="${PARITY_MSSQL_CONTAINER:-mba3849-mssql}"
PIDS="$WORK/pids"
MODE="${1:-all}"; KEEP=0
[ "${2:-}" = "--keep-running" ] && KEEP=1
mkdir -p "$PIDS" "$OUT"

stop_all() {
  for f in "$PIDS"/*.pid; do
    [ -f "$f" ] || continue
    pid="$(cat "$f")"
    if kill -0 "$pid" 2>/dev/null; then
      kill -TERM -- "-$pid" 2>/dev/null || kill -TERM "$pid" 2>/dev/null || true
      for _ in $(seq 1 50); do kill -0 "$pid" 2>/dev/null || break; sleep 0.2; done
      kill -KILL -- "-$pid" 2>/dev/null || true
    fi
    rm -f "$f"; echo "  stopped $(basename "$f" .pid) (pid $pid)"
  done
}
[ "$MODE" = "--stop" ] && { stop_all; exit 0; }

wait_200() {  # $1 name, $2 url
  for i in $(seq 1 240); do
    [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$2" || true)" = 200 ] && { echo "  $1 ready (${i}s)"; return 0; }
    sleep 1
  done
  echo "$1 not ready: $2 (log $WORK/logs/$1.log)" >&2; tail -20 "$WORK/logs/$1.log" >&2; stop_all; exit 1
}

port_free() { ! (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null || { echo "port $1 in use" >&2; stop_all; exit 1; }; }
trap 'stop_all; exit 130' INT TERM

start_legacy() {  # $1 name, $2 dir, $3 port
  port_free "$3"; mkdir -p "$WORK/logs"
  (cd "$2" && exec setsid nohup xsp4 --port "$3" --address 127.0.0.1 --root . --nonstop) > "$WORK/logs/$1.log" 2>&1 < /dev/null &
  echo $! > "$PIDS/$1.pid"
}

start_modern() {  # $1 name, $2 publish dir, $3 dll, $4 port, $5.. env assignments
  local name="$1" dir="$2" dll="$3" port="$4"; shift 4
  port_free "$port"; mkdir -p "$WORK/logs"
  (cd "$dir" && exec env "$@" ASPNETCORE_URLS="http://127.0.0.1:$port" ASPNETCORE_ENVIRONMENT=Production \
     setsid nohup dotnet "$dll" --contentRoot "$PWD") > "$WORK/logs/$name.log" 2>&1 < /dev/null &
  echo $! > "$PIDS/$name.pid"
}

sqlcmd() { docker exec "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$PARITY_SQL_PASSWORD" -C -b "$@"; }

fresh_db() {  # drop the four parity databases so every app seeds from scratch
  for db in CatalogDb_LegacyMvc CatalogDb_LegacyWebForms CatalogDb_ModernMvc CatalogDb_ModernWebForms; do
    sqlcmd -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" >/dev/null
  done
  echo "  dropped parity databases (fresh seed)"
}

legacy_db() {  # $1 Web.config, $2 database name
  sed -i -E "s/Initial Catalog=[^;]*;/Initial Catalog=$2;/" "$1"
}

publish() {
  echo "== publishing modern hosts -> $WORK/publish"
  dotnet publish "$MODERN/src/eShop.Catalog.Mvc" -c Release -o "$WORK/publish/mvc" -nologo -v q
  dotnet publish "$MODERN/src/eShop.Catalog.WebForms" -c Release -o "$WORK/publish/webforms" -nologo -v q
}

run_mode() {  # $1 mock|sql
  local m="$1" lroot lm lw mm mw
  if [ "$m" = mock ]; then lroot="$LEGACY_MOCK"; lm=5100; lw=5101; mm=5200; mw=5201; else lroot="$LEGACY_SQL"; lm=5110; lw=5111; mm=5210; mw=5211; fi
  local LMVC="$lroot/eShopLegacyMVCSolution/src/eShopLegacyMVC" LWF="$lroot/eShopLegacyWebFormsSolution/src/eShopLegacyWebForms"
  [ -d "$LMVC/bin" ] && [ -d "$LWF/bin" ] || { echo "legacy $m build missing under $lroot: run run-legacy.sh first" >&2; exit 1; }
  echo "== $m mode"
  local menv=(UseMockData=true)
  if [ "$m" = sql ]; then
    : "${PARITY_SQL_PASSWORD:?sql mode needs PARITY_SQL_PASSWORD}"
    fresh_db
    legacy_db "$LMVC/Web.config" CatalogDb_LegacyMvc; legacy_db "$LWF/Web.config" CatalogDb_LegacyWebForms
    local cs="Server=127.0.0.1,1433;User Id=sa;Password=$PARITY_SQL_PASSWORD;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True"
    start_modern "modern-mvc-$m" "$WORK/publish/mvc" eShop.Catalog.Mvc.dll "$mm" UseMockData=false "ConnectionStrings__CatalogDBContext=$cs;Database=CatalogDb_ModernMvc"
    start_modern "modern-webforms-$m" "$WORK/publish/webforms" eShop.Catalog.WebForms.dll "$mw" UseMockData=false "ConnectionStrings__CatalogDBContext=$cs;Database=CatalogDb_ModernWebForms"
  else
    start_modern "modern-mvc-$m" "$WORK/publish/mvc" eShop.Catalog.Mvc.dll "$mm" "${menv[@]}"
    start_modern "modern-webforms-$m" "$WORK/publish/webforms" eShop.Catalog.WebForms.dll "$mw" "${menv[@]}"
  fi
  start_legacy "legacy-mvc-$m" "$LMVC" "$lm"
  start_legacy "legacy-webforms-$m" "$LWF" "$lw"
  wait_200 "modern-mvc-$m" "http://127.0.0.1:$mm/healthz/ready"
  wait_200 "modern-webforms-$m" "http://127.0.0.1:$mw/healthz/ready"
  # readiness probes that do not render the catalog list (the legacy mock mutates on first list render)
  wait_200 "legacy-mvc-$m" "http://127.0.0.1:$lm/api/brands"
  wait_200 "legacy-webforms-$m" "http://127.0.0.1:$lw/About.aspx"
  local rc=0
  if [ "${PARITY_NO_REPLAY:-0}" = 1 ]; then
    if [ "$KEEP" = 1 ]; then echo "  serving only (PARITY_NO_REPLAY=1); stop with --stop"; else stop_all; fi
    return 0
  fi
  mkdir -p "$OUT/$m" && cp "$OUT/HEAD" "$OUT/$m/HEAD"   # report.py only uses results replayed on the current HEAD
  echo " mvc:";      python3 "$HERE/replay.py" --app mvc --mode "$m" --legacy "http://127.0.0.1:$lm" --modern "http://127.0.0.1:$mm" --out "$OUT/$m/mvc" || rc=1
  echo " webforms:"; python3 "$HERE/replay.py" --app webforms --mode "$m" --legacy "http://127.0.0.1:$lw" --modern "http://127.0.0.1:$mw" --out "$OUT/$m/webforms" || rc=1
  [ "$KEEP" = 1 ] || stop_all
  return $rc
}

stop_all
[ "${PARITY_SKIP_PUBLISH:-0}" = 1 ] || publish
git -C "$MODERN" rev-parse HEAD > "$OUT/HEAD"
rc=0
case "$MODE" in
  mock|sql) run_mode "$MODE" || rc=1;;
  all) run_mode mock || rc=1; run_mode sql || rc=1;;
  *) echo "usage: $0 [mock|sql|all] [--keep-running] | --stop" >&2; exit 2;;
esac
echo "== results in $OUT (HEAD $(cat "$OUT/HEAD"))"
exit $rc
