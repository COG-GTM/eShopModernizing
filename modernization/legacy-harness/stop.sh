#!/usr/bin/env bash
# Stops harness-started xsp4 processes by PID file and by owning PID of the port (never pkill xsp broadly).
# Usage: stop.sh [all|mvc|webforms]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
WHICH="${1:-all}"
stop_one() {
  local app="$1" port pidfile pids
  port="$(app_port "$app")"; pidfile="$LEGACY_ROOT/$app.pid"
  pids="$( [ -f "$pidfile" ] && cat "$pidfile" || true) $(ss -ltnpH "sport = :$port" 2>/dev/null | grep -o 'pid=[0-9]*' | cut -d= -f2 || true)"
  for p in $pids; do kill "$p" 2>/dev/null || true; done
  for _ in $(seq 1 20); do ss -ltn "sport = :$port" | grep -q LISTEN || break; sleep 0.5; done
  for p in $pids; do kill -9 "$p" 2>/dev/null || true; done
  rm -f "$pidfile"; echo "$app stopped (:$port)"
}
case "$WHICH" in all) stop_one mvc; stop_one webforms;; mvc|webforms) stop_one "$WHICH";; *) exit 2;; esac
