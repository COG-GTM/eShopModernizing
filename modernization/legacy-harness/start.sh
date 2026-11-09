#!/usr/bin/env bash
# Starts legacy apps under xsp4 (MVC :5100, WebForms :5101) in the background and waits for HTTP 200.
# Usage: start.sh [all|mvc|webforms]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
WHICH="${1:-all}"
start_one() {
  local app="$1" dir port pidfile
  dir="$(app_dir "$app")"; port="$(app_port "$app")"; pidfile="$LEGACY_ROOT/$app.pid"
  [ -d "$dir/bin" ] || { echo "$app not built; run setup.sh first"; exit 1; }
  if ss -ltn "sport = :$port" | grep -q LISTEN; then echo "port $port already in use"; exit 1; fi
  cd "$dir"
  MONO_IOMAP=all setsid nohup xsp4 --port "$port" --address 127.0.0.1 --nonstop --root "$dir" \
    > "$LEGACY_ROOT/$app.log" 2>&1 < /dev/null &
  echo $! > "$pidfile"
  for _ in $(seq 1 180); do
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 300 "http://127.0.0.1:$port/" || true)
    [ "$code" = "200" ] && { echo "$app up on :$port (pid $(cat "$pidfile"))"; return 0; }
    sleep 1
  done
  echo "$app failed to start; see $LEGACY_ROOT/$app.log"; tail -30 "$LEGACY_ROOT/$app.log"; exit 1
}
case "$WHICH" in all) start_one mvc; start_one webforms;; mvc|webforms) start_one "$WHICH";; *) exit 2;; esac
