#!/usr/bin/env bash
# (Re)starts a legacy app under xsp4 and blocks until the catalog page answers 200.
#   usage: start-legacy.sh mvc|webforms   (MVC on 5100, WebForms on 5101; logs in $LEGACY_ROOT/xsp-<app>.log)
# Restarting is how the replay harness resets the singleton in-memory mock catalog.
set -euo pipefail
LEGACY_ROOT="${LEGACY_ROOT:-/tmp}"
case "${1:-}" in
  mvc) dir="$LEGACY_ROOT/legacy-mvc/src/eShopLegacyMVC"; port=5100; probe="/" ;;
  webforms) dir="$LEGACY_ROOT/legacy-wf/src/eShopLegacyWebForms"; port=5101; probe="/" ;;
  *) echo "usage: $0 mvc|webforms" >&2; exit 2 ;;
esac
pkill -u "$(id -u)" -f "xsp4.exe --port $port" 2>/dev/null || true
for _ in $(seq 1 50); do pgrep -u "$(id -u)" -f "xsp4.exe --port $port" >/dev/null || break; sleep 0.2; done
cd "$dir"
nohup xsp4 --port "$port" --nonstop > "$LEGACY_ROOT/xsp-$1.log" 2>&1 &
for _ in $(seq 1 180); do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 120 "http://localhost:$port$probe" || true)
  [ "$code" = "200" ] && { echo "$1 ready on :$port"; exit 0; }
  sleep 1
done
echo "$1 did not become ready on :$port" >&2; exit 1
