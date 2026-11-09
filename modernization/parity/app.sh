#!/usr/bin/env bash
# Start/stop the .NET 8 storefront apps for parity runs (mock mode, fresh in-memory catalog per start).
# Usage: app.sh start|stop|restart|status [all|mvc|webforms]
# Env: DOTNET, CONFIGURATION (Release), MVC_PORT (5200), WEBFORMS_PORT (5201), PARITY_RUN_DIR (logs + pid files).
set -euo pipefail
PARITY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$PARITY_DIR/../.." && pwd)"
SRC_DIR="$REPO_DIR/net8/src"
RUN_DIR="${PARITY_RUN_DIR:-$PARITY_DIR/.run}"
CONFIGURATION="${CONFIGURATION:-Release}"
MVC_PORT="${MVC_PORT:-5200}"
WEBFORMS_PORT="${WEBFORMS_PORT:-5201}"
if [ -z "${DOTNET:-}" ]; then
  if [ -x "$HOME/.dotnet/dotnet" ]; then DOTNET="$HOME/.dotnet/dotnet"; else DOTNET="dotnet"; fi
fi
mkdir -p "$RUN_DIR"

project() { case "$1" in mvc) echo EShop.Storefront.Mvc ;; webforms) echo EShop.Storefront.WebForms ;; esac; }
port() { case "$1" in mvc) echo "$MVC_PORT" ;; webforms) echo "$WEBFORMS_PORT" ;; esac; }

port_pids() { ss -ltnpH "sport = :$1" 2>/dev/null | grep -o 'pid=[0-9]*' | cut -d= -f2 | sort -u || true; }

stop_app() {
  local app="$1" p pidfile pid
  p="$(port "$app")"; pidfile="$RUN_DIR/$app.pid"
  if [ -f "$pidfile" ]; then
    pid="$(cat "$pidfile")"
    kill "$pid" 2>/dev/null || true
    rm -f "$pidfile"
  fi
  for _ in $(seq 1 50); do [ -z "$(port_pids "$p")" ] && break; sleep 0.2; done
  # A listener left over from an earlier parity run (no pid file) is killed only if it is this app's dll;
  # anything else on the port is reported and left alone.
  for pid in $(port_pids "$p"); do
    if tr '\0' ' ' < "/proc/$pid/cmdline" 2>/dev/null | grep -q "$(project "$app").dll"; then
      kill -9 "$pid" 2>/dev/null || true
    else
      echo "port $p is held by an unrelated process (pid $pid); not stopping it" >&2
    fi
  done
  echo "$app stopped (:$p)"
}

start_app() {
  local app="$1" proj p dir dll pid code
  proj="$(project "$app")"; p="$(port "$app")"; dir="$SRC_DIR/$proj"
  dll="bin/$CONFIGURATION/net8.0/$proj.dll"
  [ -f "$dir/$dll" ] || { echo "missing $dir/$dll (run: $DOTNET build net8/EShopStorefront.sln -c $CONFIGURATION)" >&2; exit 1; }
  [ -z "$(port_pids "$p")" ] || { echo "port $p already in use; run app.sh stop $app" >&2; exit 1; }
  # Absolute --contentRoot from the project dir: a relative content root resolves wwwroot under bin/ and static assets 404.
  # Every fd is redirected and the subshell execs, so no caller pipe stays open behind the server.
  (cd "$dir" && exec env ASPNETCORE_ENVIRONMENT=Production UseMockData=true UseCustomizationData=false \
    setsid nohup "$DOTNET" "$dll" --urls "http://127.0.0.1:$p" --contentRoot "$dir") \
    > "$RUN_DIR/$app.log" 2>&1 < /dev/null &
  echo $! > "$RUN_DIR/$app.pid"
  pid="$(cat "$RUN_DIR/$app.pid")"
  for _ in $(seq 1 120); do
    code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$p/" || true)"
    if [ "$code" != "000" ]; then echo "$app up on :$p (pid $pid, GET / -> $code)"; return 0; fi
    kill -0 "$pid" 2>/dev/null || { echo "$app exited; see $RUN_DIR/$app.log" >&2; tail -20 "$RUN_DIR/$app.log" >&2; exit 1; }
    sleep 0.5
  done
  echo "$app did not answer on :$p within 60s; see $RUN_DIR/$app.log" >&2; exit 1
}

CMD="${1:-}"; WHICH="${2:-all}"
case "$WHICH" in all) APPS="mvc webforms" ;; mvc|webforms) APPS="$WHICH" ;; *) echo "unknown app: $WHICH" >&2; exit 2 ;; esac
for app in $APPS; do
  case "$CMD" in
    start) start_app "$app" ;;
    stop) stop_app "$app" ;;
    restart) stop_app "$app"; start_app "$app" ;;
    status) p="$(port "$app")"; pids="$(port_pids "$p")"; echo "$app :$p ${pids:+up (pid $pids)}${pids:-down}" ;;
    *) echo "usage: app.sh start|stop|restart|status [all|mvc|webforms]" >&2; exit 2 ;;
  esac
done
