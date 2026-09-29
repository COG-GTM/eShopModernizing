#!/usr/bin/env bash
# End-to-end test of canary/cutover.sh against a real nginx and two mock backends.
# Runs unprivileged: the stage files are copied with their ports and log paths rewritten.
# Requires: nginx, curl, python3, flock.
# shellcheck disable=SC2015,SC2016,SC2012
set -euo pipefail

TESTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$TESTS_DIR/../.." && pwd)"
CUTOVER="$REPO_ROOT/canary/cutover.sh"
STAGES=(0 25 50 75 100)

PROXY_PORT="${E2E_PROXY_PORT:-18080}"
FW_PORT="${E2E_FW_PORT:-15001}"
CORE_PORT_E2E="${E2E_CORE_PORT:-15002}"

for bin in nginx curl python3 flock; do
    command -v "$bin" >/dev/null 2>&1 || { echo "skip - $bin not installed"; exit 0; }
done

TMP="$(mktemp -d)"
NGINX=(nginx -p "$TMP/nginx" -c "$TMP/nginx/main.conf")
declare -A PIDS=()

cleanup() {
    "${NGINX[@]}" -s quit >/dev/null 2>&1 || true
    local pid
    for pid in "${PIDS[@]}"; do kill "$pid" >/dev/null 2>&1 || true; done
    if [[ "${E2E_KEEP_TMP:-0}" == 1 ]]; then echo "kept $TMP"; else rm -rf "$TMP"; fi
}
trap cleanup EXIT

mkdir -p "$TMP/stages" "$TMP/nginx/conf.d" "$TMP/nginx/logs" "$TMP/control" "$TMP/state"
for s in "${STAGES[@]}"; do
    sed -e "s/localhost:5000/127.0.0.1:15000/; s/localhost:5001/127.0.0.1:$FW_PORT/; s/localhost:5002/127.0.0.1:$CORE_PORT_E2E/" \
        -e "s/listen 80;/listen $PROXY_PORT;/" -e "s#/var/log/nginx/#$TMP/nginx/logs/#g" \
        "$REPO_ROOT/nginx-${s}percent.conf" >"$TMP/stages/nginx-${s}percent.conf"
done
cat >"$TMP/nginx/main.conf" <<CONF
pid $TMP/nginx/nginx.pid;
error_log $TMP/nginx/logs/error.log;
worker_processes 2;
events {}
http {
    client_body_temp_path $TMP/nginx/cb;
    proxy_temp_path $TMP/nginx/px;
    fastcgi_temp_path $TMP/nginx/fc;
    uwsgi_temp_path $TMP/nginx/uw;
    scgi_temp_path $TMP/nginx/sc;
    access_log off;
    server { listen 127.0.0.1:$((PROXY_PORT + 1)); return 204; }
    include $TMP/nginx/conf.d/*.conf;
}
CONF

start_backend() {
    local name=$1 port=$2
    python3 "$TESTS_DIR/mock_backend.py" "$port" "$name" "$TMP/control" &
    PIDS[$name]=$!
    for _ in $(seq 1 50); do
        curl -sf "http://127.0.0.1:$port/api/health" >/dev/null 2>&1 && return 0
        sleep 0.1
    done
    echo "backend $name did not start"
    exit 1
}
stop_backend() {
    kill "${PIDS[$1]}" && wait "${PIDS[$1]}" 2>/dev/null || true
    unset "PIDS[$1]"
}

start_backend framework "$FW_PORT"
start_backend core "$CORE_PORT_E2E"
"${NGINX[@]}" 2>/dev/null

export CANARY_ENV_FILE=/dev/null
export CANARY_STAGE_DIR="$TMP/stages"
export NGINX_CONF_TARGET="$TMP/nginx/conf.d/eshop.conf"
export NGINX_TEST_CMD="nginx -p $TMP/nginx -c $TMP/nginx/main.conf -t"
export NGINX_RELOAD_CMD="nginx -p $TMP/nginx -c $TMP/nginx/main.conf -s reload"
export CANARY_STATE_DIR="$TMP/state"
export CANARY_ACCESS_LOG="$TMP/nginx/logs/eshop-canary.log"
export CANARY_BASE_URL="http://127.0.0.1:$PROXY_PORT"
export CORE_URL="http://127.0.0.1:$CORE_PORT_E2E"
export FRAMEWORK_URL="http://127.0.0.1:$FW_PORT"
export CORE_PORT="$CORE_PORT_E2E"
export FRAMEWORK_PORT="$FW_PORT"
export CANARY_SOAK_SECONDS=6
export CANARY_WINDOW_SECONDS=60
export CANARY_CHECK_INTERVAL=2
export CANARY_PROBE_INTERVAL=0.05
export CANARY_VERIFY_TIMEOUT=10
export CANARY_ROLLBACK_WATCH_SECONDS=2
export MIN_REQUESTS=20
export MIN_BACKEND_REQUESTS=5

failures=0
pass() { echo "ok   - $*"; }
not_ok() { echo "FAIL - $*"; failures=$((failures + 1)); }

# run <expected-exit> <description> -- cutover args...
run() {
    local expected=$1 desc=$2 rc=0
    shift 3
    "$CUTOVER" "$@" </dev/null >"$TMP/last.out" 2>&1 || rc=$?
    if [[ "$rc" == "$expected" ]]; then
        pass "$desc (exit $rc)"
    else
        not_ok "$desc: expected exit $expected, got $rc"
        sed 's/^/    /' "$TMP/last.out"
    fi
}

installed() { sed -n 's/^[[:space:]]*set \$canary_stage \([0-9]*\);.*/\1/p' "$NGINX_CONF_TARGET" 2>/dev/null || echo none; }
live() { curl -s -o /dev/null -D - "$CANARY_BASE_URL/Catalog/" | tr -d '\r' | sed -n 's/^X-Canary-Stage: //Ip'; }
served_by() { curl -s -o /dev/null -D - -H "X-Forwarded-For: $1" "$CANARY_BASE_URL/Catalog/" | tr -d '\r' | sed -n 's/^X-Served-By: //Ip'; }

expect_stage() {
    local want=$1 got_file got_live
    got_file="$(installed)"
    got_live="$(live)"
    if [[ "$got_file" == "$want" && "$got_live" == "$want" ]]; then
        pass "stage $want installed and served"
    else
        not_ok "expected stage $want, installed=$got_file live=$got_live"
    fi
}

expect_output() {
    if grep -Eq "$1" "$TMP/last.out"; then pass "output: $2"; else not_ok "output: $2 (pattern '$1')"; sed 's/^/    /' "$TMP/last.out"; fi
}

# Runs "$@" in the background ~1s after the next "advance FROM -> TO applied" history entry.
# Sets HELPER_PID.
after_next_apply() {
    local from=$1 to=$2 before
    shift 2
    before="$(applied_count "$from" "$to")"
    (
        for _ in $(seq 1 200); do
            (($(applied_count "$from" "$to") > before)) && break
            sleep 0.1
        done
        sleep 1
        "$@"
    ) &
    HELPER_PID=$!
}
applied_count() {
    local n
    n="$(grep -c $'\tadvance\t'"$1"$'\t'"$2"$'\tapplied' "$CANARY_STATE_DIR/history.tsv" 2>/dev/null || true)"
    echo "${n:-0}"
}
inject_core_failure() { touch "$TMP/control/core.fail"; }
kill_core() { kill "${PIDS[core]}"; }

clients() { for i in $(seq 1 "$1"); do echo "203.0.113.$((i % 250 + 1)), 10.0.$((i / 250)).1"; done; }
core_clients() {
    local ip
    while read -r ip; do
        if [[ "$(served_by "$ip")" == core ]]; then echo "$ip"; fi
    done < <(clients 200)
}

echo "# bootstrap and guard rails"
run 1 "advance without an installed stage is refused" -- advance --yes
run 1 "bootstrap straight to 25 is refused without --force" -- advance --to 25 --yes
run 1 "advance without --yes on a non-tty is refused" -- advance --to 0
run 0 "bootstrap to stage 0" -- advance --to 0 --yes
expect_stage 0
[[ "$(served_by 198.51.100.7)" == framework ]] && pass "stage 0 serves /Catalog/ from the framework app" || not_ok "stage 0 not on framework"

echo "# dry run"
run 0 "advance --dry-run" -- advance --dry-run
expect_output '^\+    server 127\.0\.0\.1:'"$CORE_PORT_E2E"' weight=1;' "dry run shows the upstream diff"
expect_stage 0

echo "# healthy advance to 25"
run 0 "advance 0 -> 25" -- advance --yes --reason e2e
expect_stage 25
expect_output 'gates ok +final stage=25' "gates evaluated at stage 25"
core_clients >"$TMP/core-at-25"
n25="$(wc -l <"$TMP/core-at-25")"
((n25 > 20 && n25 < 90)) && pass "stage 25 sends $n25/200 clients to .NET Core" || not_ok "stage 25 sent $n25/200 clients to .NET Core"
sticky="$(for _ in $(seq 1 10); do served_by 203.0.113.9; done | sort -u | wc -l)"
[[ "$sticky" == 1 ]] && pass "a client stays on one backend" || not_ok "client affinity broken ($sticky backends)"

echo "# refusals"
run 1 "skipping 25 -> 75 is refused" -- advance --to 75 --yes
run 1 "rollback forward is refused" -- rollback --to 75
expect_stage 25

echo "# nginx rejects the candidate config"
cp "$TMP/stages/nginx-50percent.conf" "$TMP/nginx-50.good"
echo "bogus_directive on;" >>"$TMP/stages/nginx-50percent.conf"
run 3 "invalid stage 50 config is rejected and reverted" -- advance --yes
cmp -s "$NGINX_CONF_TARGET" "$TMP/stages/nginx-25percent.conf" && pass "stage 25 file restored byte-for-byte" || not_ok "stage 25 file not restored"
expect_stage 25
cp "$TMP/nginx-50.good" "$TMP/stages/nginx-50percent.conf"

echo "# .NET Core returns 500s during the soak"
touch "$TMP/control/core.fail"
run 2 "preflight blocks advance while .NET Core catalog fails" -- advance --yes
rm -f "$TMP/control/core.fail"
after_next_apply 25 50 inject_core_failure
run 4 "5xx during soak triggers automatic rollback" -- advance --yes
wait "$HELPER_PID"
rm -f "$TMP/control/core.fail"
expect_output 'ROLLING BACK to stage 25' "rollback reason logged"
expect_stage 25

echo "# .NET Core latency regression"
echo 0.4 >"$TMP/control/core.delay"
export MAX_P95_MS=200
run 4 "p95 above MAX_P95_MS triggers automatic rollback" -- advance --yes
unset MAX_P95_MS
expect_output 'p95 [0-9]+ms > 200ms' "latency gate reason logged"
rm -f "$TMP/control/core.delay"
expect_stage 25

echo "# .NET Core goes down during the soak (masked by nginx failover)"
after_next_apply 25 50 kill_core
export MAX_CONSECUTIVE_PROBE_FAILURES=1000
run 4 "losing .NET Core mid-soak triggers automatic rollback" -- advance --yes
unset MAX_CONSECUTIVE_PROBE_FAILURES
wait "$HELPER_PID"
expect_output 'failover rate|below 50%' "failover / missing-share gate reason logged"
expect_stage 25
run 2 "preflight blocks advance while .NET Core is down" -- advance --yes
expect_stage 25
start_backend core "$CORE_PORT_E2E"

echo "# healthy advance to 100"
run 0 "advance 25 -> 50" -- advance --yes
expect_stage 50
core_clients >"$TMP/core-at-50"
moved_back="$(comm -23 <(sort "$TMP/core-at-25") <(sort "$TMP/core-at-50") | wc -l)"
[[ "$moved_back" == 0 ]] && pass "no client moved back to the framework app between 25 and 50 ($(wc -l <"$TMP/core-at-50")/200 on .NET Core)" || not_ok "$moved_back clients moved back to framework at stage 50"
run 0 "report at stage 50" -- report
expect_output '\.NET Core +[0-9]+ +[0-9.]+%' "report prints the split"
run 0 "advance 50 -> 75" -- advance --yes
expect_stage 75
run 0 "advance 75 -> 100" -- advance --yes
expect_stage 100
[[ "$(served_by 198.51.100.7)" == core ]] && pass "stage 100 serves /Catalog/ from .NET Core" || not_ok "stage 100 not on .NET Core"
run 0 "advance at 100 is a no-op" -- advance --yes
run 0 "status" -- status
expect_output 'Gates +: ok' "status reports healthy gates"

echo "# watch with auto-rollback"
touch "$TMP/control/core.fail"
run 4 "watch --auto-rollback rolls back one stage on breach" -- watch --duration 5 --auto-rollback
rm -f "$TMP/control/core.fail"
expect_stage 75

echo "# manual rollback"
stop_backend framework
run 2 "rollback onto a dead framework app is refused" -- rollback --to 0 --duration 0
expect_stage 75
start_backend framework "$FW_PORT"
run 0 "rollback one stage" -- rollback
expect_stage 50
run 0 "emergency rollback to 0" -- rollback --to 0 --reason "e2e emergency"
expect_stage 0
latest="$(ls -1t "$CANARY_STATE_DIR"/backups/eshop-*-stage50.conf | head -n 1)"
run 0 "restore an exact backup" -- restore --file "$latest" --yes
expect_stage 50
run 0 "history" -- history
expect_output 'rollback +50 +0 +ok +e2e emergency' "history records the emergency rollback"

echo "# deprecated wrapper"
rc=0
"$REPO_ROOT/migrate-traffic.sh" 75 --dry-run </dev/null >"$TMP/last.out" 2>&1 || rc=$?
[[ "$rc" == 0 ]] && pass "migrate-traffic.sh 75 forwards to advance --to 75 (exit 0)" || not_ok "migrate-traffic.sh 75 exited $rc"
expect_output 'Plan: stage 50 -> 75' "wrapper prints the cutover plan"
expect_stage 50

if ((failures > 0)); then
    echo "$failures check(s) failed"
    exit 1
fi
echo "all e2e checks passed"
