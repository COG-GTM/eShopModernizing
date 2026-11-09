#!/usr/bin/env bash
# Replays the recorded legacy transcripts (modernization/baseline/transcripts) against the containers.
#   replay.sh mock [--no-build] [--tests]   expects 138/138 (mvc 86 + webforms 52) against the mock-mode baseline
#   replay.sh sql  [--no-build] [--tests]   checks seed counts, replays the same transcripts and compares the outcome with
#                                           legacy SQL mode (modernization/baseline/sql-mode) via sql_parity.py
# --tests also runs the characterization suite (modernization/tools/tests) against freshly reset containers (mock mode only).
# Reports: net8/docker/results/<mode>-<app>.json (+ sql-parity.json). Exit code 0 only if the acceptance holds.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
MODE="${1:?usage: replay.sh <mock|sql> [--no-build] [--tests]}"; shift
UP_ARGS=(); RUN_TESTS=0
for a in "$@"; do
  case "$a" in
    --no-build) UP_ARGS=(--no-build) ;;
    --tests) RUN_TESTS=1 ;;
    *) echo "unknown option: $a" >&2; exit 2 ;;
  esac
done
[[ "$MODE" == mock || "$MODE" == sql ]] || { echo "mode must be mock or sql" >&2; exit 2; }
python3 -c 'import requests, bs4' 2>/dev/null || { echo "pip install -r modernization/tools/requirements.txt" >&2; exit 2; }

STACK="$HERE/stack.sh"
RESULTS="$HERE/results"; mkdir -p "$RESULTS"
"$STACK" "$MODE" up "${UP_ARGS[@]}"

status=0
if [[ "$MODE" == sql ]]; then
  "$STACK" sql reset all
  "$STACK" sql seed | tee "$RESULTS/sql-seed.csv"
fi

for app in mvc webforms; do
  python3 "$REPO/modernization/tools/transcript.py" replay --app "$app" \
    --base-url "$("$STACK" "$MODE" url "$app")" \
    --reset-cmd "'$STACK' $MODE reset $app" \
    --report "$RESULTS/$MODE-$app.json" || { [[ "$MODE" == mock ]] && status=1; }
done

if [[ "$MODE" == sql ]]; then
  python3 "$HERE/sql_parity.py" --results "$RESULTS" --seed "$RESULTS/sql-seed.csv" --report "$RESULTS/sql-parity.json" || status=1
fi

if [[ "$RUN_TESTS" == 1 ]]; then
  if [[ "$MODE" == mock ]]; then
    "$STACK" mock reset all
    MVC_BASE_URL="$("$STACK" mock url mvc)" WEBFORMS_BASE_URL="$("$STACK" mock url webforms)" \
      python3 -m pytest "$REPO/modernization/tools/tests" -q -p no:cacheprovider \
      --junitxml "$RESULTS/mock-characterization.xml" || status=1
  else
    echo "--tests: the characterization suite assumes mock-mode data (CONTRACT.md); skipped in sql mode" >&2
  fi
fi
exit "$status"
