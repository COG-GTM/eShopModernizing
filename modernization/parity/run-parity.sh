#!/usr/bin/env bash
# Parity run: build the .NET 8 solution, replay all legacy baseline transcripts against fresh app instances
# (mvc 86 on :5200, webforms 52 on :5201), run the 77-test characterization suite, then write results/*.json
# and PARITY-REPORT.md (pass/total vs legacy). Exit 0 only when everything matches or is an accepted difference.
# Usage: run-parity.sh [--no-build] [--strict] [--keep-running]
set -uo pipefail
PARITY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$PARITY_DIR/../.." && pwd)"
RESULTS="$PARITY_DIR/results"
PYTHON="${PYTHON:-python3}"
CONFIGURATION="${CONFIGURATION:-Release}"; export CONFIGURATION
MVC_PORT="${MVC_PORT:-5200}"; WEBFORMS_PORT="${WEBFORMS_PORT:-5201}"; export MVC_PORT WEBFORMS_PORT
if [ -z "${DOTNET:-}" ]; then
  if [ -x "$HOME/.dotnet/dotnet" ]; then DOTNET="$HOME/.dotnet/dotnet"; else DOTNET="dotnet"; fi
fi
export DOTNET
BUILD=1; STRICT=""; KEEP=0
for a in "$@"; do
  case "$a" in
    --no-build) BUILD=0 ;;
    --strict) STRICT="--strict" ;;
    --keep-running) KEEP=1 ;;
    *) echo "unknown option: $a" >&2; exit 2 ;;
  esac
done
APP="$PARITY_DIR/app.sh"
mkdir -p "$RESULTS"
"$PYTHON" -c "import requests, bs4, pytest" 2>/dev/null \
  || { echo "missing python deps: pip install -r modernization/tools/requirements.txt" >&2; exit 2; }
cleanup() { [ "$KEEP" = 1 ] || "$APP" stop all >/dev/null 2>&1 || true; }
trap cleanup EXIT

cd "$REPO_DIR"
if [ "$BUILD" = 1 ]; then
  echo "== dotnet build net8/EShopStorefront.sln -c $CONFIGURATION"
  "$DOTNET" build net8/EShopStorefront.sln -c "$CONFIGURATION" -nologo -v q > "$RESULTS/build.log" 2>&1 \
    || { tail -30 "$RESULTS/build.log" >&2; echo "build failed (results/build.log)" >&2; exit 1; }
  grep -E "Warning\(s\)|Error\(s\)" "$RESULTS/build.log" | tail -2
fi
"$APP" stop all

# transcript.py runs --reset-cmd before replaying, so each app set is replayed against a freshly started process.
for app in mvc webforms; do
  p="$MVC_PORT"; [ "$app" = webforms ] && p="$WEBFORMS_PORT"
  echo "== replay $app transcripts against :$p"
  "$PYTHON" modernization/tools/transcript.py replay --app "$app" --base-url "http://127.0.0.1:$p" $STRICT \
    --reset-cmd "'$APP' restart $app" --report "$RESULTS/$app.json" | tail -1
done

echo "== characterization suite against fresh apps"
"$APP" restart all
MVC_BASE_URL="http://127.0.0.1:$MVC_PORT" WEBFORMS_BASE_URL="http://127.0.0.1:$WEBFORMS_PORT" \
  "$PYTHON" -m pytest modernization/tools/tests -q -p no:cacheprovider -rA \
  --junitxml "$RESULTS/characterization.xml" > "$RESULTS/characterization.txt" 2>&1
tail -1 "$RESULTS/characterization.txt"

"$PYTHON" "$PARITY_DIR/report.py"
