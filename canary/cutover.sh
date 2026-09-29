#!/usr/bin/env bash
# Scripted canary cutover of eShop /Catalog/ traffic from the modernized .NET Framework app
# (port 5001) to the .NET Core app (port 5002) using the nginx-<N>percent.conf stage files.
#
# Operator documentation: CANARY-CUTOVER-RUNBOOK.md (repository root).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

CANARY_ENV_FILE="${CANARY_ENV_FILE:-$SCRIPT_DIR/canary.env}"
if [[ -f "$CANARY_ENV_FILE" ]]; then
    # shellcheck source=/dev/null
    source "$CANARY_ENV_FILE"
fi

STAGES=(0 25 50 75 100)

CANARY_STAGE_DIR="${CANARY_STAGE_DIR:-$REPO_ROOT}"
NGINX_CONF_TARGET="${NGINX_CONF_TARGET:-/etc/nginx/conf.d/eshop.conf}"
NGINX_TEST_CMD="${NGINX_TEST_CMD:-nginx -t}"
NGINX_RELOAD_CMD="${NGINX_RELOAD_CMD:-nginx -s reload}"
CANARY_STATE_DIR="${CANARY_STATE_DIR:-$SCRIPT_DIR/state}"
CANARY_ACCESS_LOG="${CANARY_ACCESS_LOG:-/var/log/nginx/eshop-canary.log}"
CANARY_LOG_TAIL_LINES="${CANARY_LOG_TAIL_LINES:-200000}"
CANARY_BACKUP_KEEP="${CANARY_BACKUP_KEEP:-50}"

CANARY_BASE_URL="${CANARY_BASE_URL:-http://localhost}"
CORE_URL="${CORE_URL:-http://localhost:5002}"
FRAMEWORK_URL="${FRAMEWORK_URL:-http://localhost:5001}"
CORE_PORT="${CORE_PORT:-5002}"
FRAMEWORK_PORT="${FRAMEWORK_PORT:-5001}"
FRAMEWORK_HEALTH_PATH="${FRAMEWORK_HEALTH_PATH:-/}"
CANARY_CATALOG_PATH="${CANARY_CATALOG_PATH:-/Catalog/}"
CANARY_PROBE_PATHS="${CANARY_PROBE_PATHS:-/Catalog/ /api/health}"
CANARY_HTTP_TIMEOUT="${CANARY_HTTP_TIMEOUT:-10}"

CANARY_SOAK_SECONDS="${CANARY_SOAK_SECONDS:-900}"
CANARY_WINDOW_SECONDS="${CANARY_WINDOW_SECONDS:-300}"
CANARY_CHECK_INTERVAL="${CANARY_CHECK_INTERVAL:-30}"
CANARY_PROBE_INTERVAL="${CANARY_PROBE_INTERVAL:-2}"
CANARY_VERIFY_TIMEOUT="${CANARY_VERIFY_TIMEOUT:-30}"
CANARY_ROLLBACK_WATCH_SECONDS="${CANARY_ROLLBACK_WATCH_SECONDS:-120}"
CANARY_REQUIRE_TRAFFIC="${CANARY_REQUIRE_TRAFFIC:-1}"

MAX_5XX_PCT="${MAX_5XX_PCT:-1.0}"
MAX_P95_MS="${MAX_P95_MS:-5000}"
MAX_P95_RATIO="${MAX_P95_RATIO:-2.0}"
P95_RATIO_FLOOR_MS="${P95_RATIO_FLOOR_MS:-100}"
MAX_FAILOVER_PCT="${MAX_FAILOVER_PCT:-1.0}"
MAX_CONSECUTIVE_PROBE_FAILURES="${MAX_CONSECUTIVE_PROBE_FAILURES:-3}"
MIN_REQUESTS="${MIN_REQUESTS:-50}"
MIN_BACKEND_REQUESTS="${MIN_BACKEND_REQUESTS:-20}"
SPLIT_TOLERANCE_PCT="${SPLIT_TOLERANCE_PCT:-15}"

readonly E_USAGE=1 E_PREFLIGHT=2 E_CONFIG=3 E_GATE=4 E_VERIFY=5 E_LOCK=6

OPT_TO=""
OPT_YES=0
OPT_FORCE=0
OPT_DRY_RUN=0
OPT_AUTO_ROLLBACK=""
OPT_SOAK=""
OPT_DURATION=""
OPT_WINDOW=""
OPT_REASON=""
OPT_FILE=""

GATE_REASON=""
LAST_BACKUP=""
GATE_SUMMARY=""

usage() {
    cat <<USAGE
Usage: $(basename "$0") <command> [options]

Commands:
  status                 Show installed stage, backend health, recent history and live gate metrics.
  preflight [--to N]     Run the pre-advance checks for the next stage (or stage N) without changing anything.
  advance [--to N]       Move to the next stage (0 -> 25 -> 50 -> 75 -> 100), then soak and watch the gates.
                         A failed gate rolls back to the previous stage automatically.
  rollback [--to N]      Move back one stage (or straight to stage N, e.g. --to 0 for a full revert).
  restore --file PATH    Re-install an exact backup (from $CANARY_STATE_DIR/backups).
  watch [--duration S]   Probe and evaluate the gates at the current stage without changing it.
  report [--window S]    One-shot traffic split / error / latency report from the canary access log.
  verify-configs         Statically validate every nginx-<N>percent.conf stage file.
  history                Show the cutover history.

Options:
  --to N                 Target stage: one of ${STAGES[*]}.
  --yes                  Do not prompt for confirmation (required when stdin is not a terminal).
  --force                Allow skipping stages / rolling back to an unhealthy backend.
  --dry-run              Print the plan and config diff, change nothing.
  --soak S               Soak duration in seconds for advance (default $CANARY_SOAK_SECONDS).
  --no-auto-rollback     advance: report a failed gate but leave the new stage in place.
  --auto-rollback        watch: roll back one stage when a gate fails.
  --duration S           watch/rollback: observation time in seconds (watch: 0 = until interrupted).
  --window S             report: size of the log window in seconds (default $CANARY_WINDOW_SECONDS).
  --reason TEXT          Free text recorded in the history (e.g. an incident or change ticket).

Exit codes: 0 ok, $E_USAGE usage/refused, $E_PREFLIGHT preflight failed, $E_CONFIG nginx config test/reload failed
(previous config restored), $E_GATE gate breached (rolled back unless --no-auto-rollback),
$E_VERIFY new stage not observed live (rolled back), $E_LOCK another cutover holds the lock.
USAGE
}

timestamp() { date -u +%Y-%m-%dT%H:%M:%SZ; }
now_epoch() { date +%s.%N; }

log() {
    local line
    line="$(timestamp) $*"
    echo "$line" >&2
    if [[ -d "$CANARY_STATE_DIR" ]]; then
        echo "$line" >>"$CANARY_STATE_DIR/cutover.log"
    fi
}

fail() {
    local code=$1
    shift
    log "ERROR: $*"
    exit "$code"
}

fgt() { awk -v a="$1" -v b="$2" 'BEGIN { exit !((a + 0) > (b + 0)) }'; }

pct_of() { awk -v a="$1" -v b="$2" 'BEGIN { if (b + 0 == 0) print "0.00"; else printf "%.2f\n", a * 100 / b }'; }

fmax() { awk -v a="$1" -v b="$2" 'BEGIN { print ((a + 0) > (b + 0)) ? a : b }'; }

is_stage() {
    local s
    for s in "${STAGES[@]}"; do
        [[ "$s" == "$1" ]] && return 0
    done
    return 1
}

stage_index() {
    local i
    for i in "${!STAGES[@]}"; do
        if [[ "${STAGES[$i]}" == "$1" ]]; then
            echo "$i"
            return 0
        fi
    done
    return 1
}

stage_file() { echo "$CANARY_STAGE_DIR/nginx-${1}percent.conf"; }

marker_stage() {
    # shellcheck disable=SC2016
    sed -n 's/^[[:space:]]*set[[:space:]]\{1,\}\$canary_stage[[:space:]]\{1,\}\([0-9]\{1,\}\);.*/\1/p' "$1" | head -n 1
}

# Effective .NET Core share (rounded %) of the catalog_migration upstream, from its server weights.
weights_core_pct() {
    awk -v cp="$CORE_PORT" '
        /^[[:space:]]*upstream[[:space:]]+catalog_migration[[:space:]]*\{/ { inb = 1; next }
        inb && /^[[:space:]]*\}/ { inb = 0 }
        inb && /^[[:space:]]*server[[:space:]]/ {
            line = $0
            sub(/#.*/, "", line)
            if (line ~ /[[:space:]](down|backup)[[:space:];]/) next
            w = 1
            if (match(line, /weight=[0-9]+/)) w = substr(line, RSTART + 7, RLENGTH - 7) + 0
            addr = line
            sub(/^[[:space:]]*server[[:space:]]+/, "", addr)
            sub(/[[:space:];].*/, "", addr)
            if (addr ~ (":" cp "$")) core += w
            total += w
        }
        END {
            if (total == 0) exit 1
            printf "%d\n", core * 100 / total + 0.5
        }' "$1"
}

# Prints the stage installed in FILE: a stage number, "none" (no file) or "unknown".
detect_stage() {
    local file=$1 s
    if [[ ! -f "$file" ]]; then
        echo none
        return
    fi
    s="$(marker_stage "$file")"
    if [[ -z "$s" ]]; then
        s="$(weights_core_pct "$file" 2>/dev/null || true)"
    fi
    if [[ -n "$s" ]] && is_stage "$s"; then
        echo "$s"
    else
        echo unknown
    fi
}

# Checks a stage file: exists, marker and weights both equal the stage number.
verify_stage_file() {
    local stage=$1 file marker weights
    file="$(stage_file "$stage")"
    if [[ ! -f "$file" ]]; then
        echo "missing $file"
        return 1
    fi
    marker="$(marker_stage "$file")"
    weights="$(weights_core_pct "$file" 2>/dev/null || echo "?")"
    if [[ "$marker" != "$stage" ]]; then
        echo "$file: \$canary_stage is '${marker:-unset}', expected $stage"
        return 1
    fi
    if [[ "$weights" != "$stage" ]]; then
        echo "$file: catalog_migration weights give ${weights}% .NET Core, expected $stage%"
        return 1
    fi
    echo "$file: ok (stage $stage, weights ${weights}% .NET Core)"
}

ensure_state_dir() {
    mkdir -p "$CANARY_STATE_DIR/backups"
}

acquire_lock() {
    exec 9>"$CANARY_STATE_DIR/lock"
    flock -n 9 || fail "$E_LOCK" "another cutover command holds $CANARY_STATE_DIR/lock"
}

state_get() {
    local key=$1
    [[ -f "$CANARY_STATE_DIR/current" ]] || return 0
    sed -n "s/^${key}=//p" "$CANARY_STATE_DIR/current" | tail -n 1
}

state_set() {
    local key=$1 value=$2 file="$CANARY_STATE_DIR/current"
    touch "$file"
    grep -v "^${key}=" "$file" >"$file.tmp" || true
    echo "${key}=${value}" >>"$file.tmp"
    mv -f "$file.tmp" "$file"
}

history_add() {
    local action=$1 from=$2 to=$3 result=$4 note=${5:-}
    printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$(timestamp)" "${SUDO_USER:-${USER:-unknown}}" \
        "$action" "$from" "$to" "$result" "${note//$'\t'/ }" >>"$CANARY_STATE_DIR/history.tsv"
}

confirm() {
    local prompt=$1 answer
    [[ "$OPT_YES" == 1 ]] && return 0
    if [[ ! -t 0 ]]; then
        fail "$E_USAGE" "refusing to continue without confirmation: stdin is not a terminal (pass --yes)"
    fi
    read -r -p "$prompt [y/N] " answer
    [[ "$answer" == y || "$answer" == Y || "$answer" == yes ]] || fail "$E_USAGE" "aborted by operator"
}

rand_client_ip() { echo "198.18.$((RANDOM % 256)).$((RANDOM % 254 + 1))"; }

# Prints "<http_code> <seconds>"; http_code is 000 when the request failed outright.
http_probe() {
    local out
    out="$(curl -sS -o /dev/null -w '%{http_code} %{time_total}' --max-time "$CANARY_HTTP_TIMEOUT" "$@" 2>/dev/null)" || true
    [[ -n "$out" ]] || out="000 0"
    echo "$out"
}

http_ok() {
    local code
    code="$(http_probe "$@" | cut -d' ' -f1)"
    [[ "$code" =~ ^[23][0-9][0-9]$ ]]
}

live_stage_header() {
    curl -sS -o /dev/null -D - --max-time "$CANARY_HTTP_TIMEOUT" -H "X-Forwarded-For: $(rand_client_ip)" \
        "$CANARY_BASE_URL$CANARY_CATALOG_PATH" 2>/dev/null |
        tr -d '\r' | awk -F': *' 'tolower($1) == "x-canary-stage" { print $2 }' | tail -n 1
}

run_nginx_cmd() {
    local what=$1 cmd=$2 out
    if out="$(bash -c "$cmd" 2>&1)"; then
        return 0
    fi
    log "$what failed: $cmd"
    printf '%s\n' "$out" | sed 's/^/    /' >&2
    return 1
}

prune_backups() {
    local old
    # shellcheck disable=SC2012
    old="$(ls -1t "$CANARY_STATE_DIR"/backups/eshop-*.conf 2>/dev/null | tail -n +"$((CANARY_BACKUP_KEEP + 1))")"
    if [[ -n "$old" ]]; then
        printf '%s\n' "$old" | xargs rm -f --
    fi
}

install_file() {
    local src=$1 tmp="$NGINX_CONF_TARGET.canary-tmp.$$"
    mkdir -p "$(dirname "$NGINX_CONF_TARGET")"
    cp "$src" "$tmp"
    mv -f "$tmp" "$NGINX_CONF_TARGET"
}

# Installs SRC as the live config: backup, nginx -t, reload. Restores the previous file on failure.
activate_file() {
    local src=$1 label=$2 from ts backup=""
    from="$(detect_stage "$NGINX_CONF_TARGET")"
    ts="$(date -u +%Y%m%dT%H%M%SZ)"
    if [[ -f "$NGINX_CONF_TARGET" ]]; then
        backup="$CANARY_STATE_DIR/backups/eshop-$ts-stage$from.conf"
        cp -p "$NGINX_CONF_TARGET" "$backup"
        log "backed up $NGINX_CONF_TARGET (stage $from) to $backup"
    fi
    LAST_BACKUP="$backup"
    install_file "$src"
    log "installed $src as $NGINX_CONF_TARGET ($label)"
    if ! run_nginx_cmd "nginx config test" "$NGINX_TEST_CMD"; then
        restore_previous "$backup"
        return 1
    fi
    if ! run_nginx_cmd "nginx reload" "$NGINX_RELOAD_CMD"; then
        restore_previous "$backup"
        run_nginx_cmd "nginx reload (after restore)" "$NGINX_RELOAD_CMD" || true
        return 1
    fi
    state_set previous_stage "$from"
    state_set switched_at "$(now_epoch)"
    prune_backups
    log "nginx reloaded"
}

restore_previous() {
    local backup=$1
    if [[ -n "$backup" ]]; then
        install_file "$backup"
        log "restored previous config from $backup"
    else
        rm -f "$NGINX_CONF_TARGET"
        log "removed $NGINX_CONF_TARGET (nothing was installed before)"
    fi
}

activate_stage() {
    local stage=$1 label=$2 check
    if ! check="$(verify_stage_file "$stage")"; then
        fail "$E_CONFIG" "$check"
    fi
    activate_file "$(stage_file "$stage")" "$label"
}

# Waits until three consecutive proxied responses carry X-Canary-Stage: STAGE.
verify_live() {
    local stage=$1 deadline seen=0 got
    deadline=$(($(date +%s) + CANARY_VERIFY_TIMEOUT))
    while (($(date +%s) < deadline)); do
        got="$(live_stage_header)"
        if [[ "$got" == "$stage" ]]; then
            seen=$((seen + 1))
            if ((seen >= 3)); then
                log "verified: $CANARY_BASE_URL$CANARY_CATALOG_PATH is served by stage $stage"
                return 0
            fi
        else
            seen=0
        fi
        sleep 1
    done
    log "stage $stage not observed at $CANARY_BASE_URL$CANARY_CATALOG_PATH within ${CANARY_VERIFY_TIMEOUT}s (last X-Canary-Stage: '${got:-none}')"
    return 1
}

# Prints: total core_n core_5xx core_p95 fw_n fw_5xx fw_p95 other_n other_5xx failovers core_p50 fw_p50
analyze_window() {
    local since=$1 stage=$2
    if [[ ! -r "$CANARY_ACCESS_LOG" ]]; then
        echo "0 0 0 0 0 0 0 0 0 0 0 0"
        return
    fi
    tail -n "$CANARY_LOG_TAIL_LINES" "$CANARY_ACCESS_LOG" |
        awk -F'\t' -v since="$since" -v stage="$stage" -v cp="$CORE_PORT" -v fp="$FRAMEWORK_PORT" -v prefix="$CANARY_CATALOG_PATH" '
            NF >= 9 && ($1 + 0) >= (since + 0) && $2 == stage && index($9, prefix) == 1 {
                n = split($4, addrs, /(, | : )/)
                port = addrs[n]
                sub(/.*:/, "", port)
                b = (port == cp) ? "core" : ((port == fp) ? "fw" : "other")
                printf "%s\t%d\t%d\t%d\n", b, $6 * 1000 + 0.5, ($3 + 0 >= 500) ? 1 : 0, (n > 1) ? 1 : 0
            }' |
        sort -t "$(printf '\t')" -k1,1 -k2,2n |
        awk -F'\t' '
            { n[$1]++; lat[$1, n[$1]] = $2; e[$1] += $3; fo += $4; tot++ }
            function q(b, p,    i) {
                if (n[b] == 0) return 0
                i = int(n[b] * p)
                if (i < n[b] * p) i++
                if (i < 1) i = 1
                return lat[b, i]
            }
            END {
                printf "%d %d %d %d %d %d %d %d %d %d %d %d\n", tot, n["core"], e["core"], q("core", 0.95),
                    n["fw"], e["fw"], q("fw", 0.95), n["other"], e["other"], fo, q("core", 0.5), q("fw", 0.5)
            }'
}

window_since() {
    local switched now
    switched="$(state_get switched_at)"
    now="$(now_epoch)"
    fmax "${switched:-0}" "$(awk -v n="$now" -v w="$1" 'BEGIN { printf "%.3f\n", n - w }')"
}

# Evaluates the rollback gates for STAGE. Returns 0 pass, 1 breach (GATE_REASON set), 2 not enough data.
evaluate_gates() {
    local stage=$1 window=${2:-$CANARY_WINDOW_SECONDS}
    local total core_n core_5xx core_p95 fw_n fw_5xx fw_p95 other_n other_5xx failovers core_p50 fw_p50
    local core_share core_err fw_err fo_pct baseline limit
    read -r total core_n core_5xx core_p95 fw_n fw_5xx fw_p95 other_n other_5xx failovers core_p50 fw_p50 \
        <<<"$(analyze_window "$(window_since "$window")" "$stage")"
    core_share="$(pct_of "$core_n" "$total")"
    core_err="$(pct_of "$core_5xx" "$core_n")"
    fw_err="$(pct_of "$fw_5xx" "$fw_n")"
    fo_pct="$(pct_of "$failovers" "$total")"
    GATE_REASON=""
    GATE_SUMMARY="stage=$stage req=$total core=${core_share}% (want ${stage}%) core5xx=${core_err}% fw5xx=${fw_err}% p95(core/fw)=${core_p95}/${fw_p95}ms failover=${fo_pct}% no-upstream5xx=$other_5xx/$other_n"

    if ((total < MIN_REQUESTS)); then
        GATE_REASON="only $total /Catalog/ requests in window (need $MIN_REQUESTS)"
        return 2
    fi
    if ((core_n >= MIN_BACKEND_REQUESTS)) && fgt "$core_err" "$MAX_5XX_PCT"; then
        GATE_REASON=".NET Core 5xx rate ${core_err}% > ${MAX_5XX_PCT}%"
        return 1
    fi
    if fgt "$fo_pct" "$MAX_FAILOVER_PCT"; then
        GATE_REASON="upstream failover rate ${fo_pct}% > ${MAX_FAILOVER_PCT}% (a backend is refusing/timing out connections)"
        return 1
    fi
    if ((other_5xx > 0)); then
        GATE_REASON="$other_5xx requests failed with no upstream available"
        return 1
    fi
    if ((core_n >= MIN_BACKEND_REQUESTS)) && fgt "$core_p95" "$MAX_P95_MS"; then
        GATE_REASON=".NET Core p95 ${core_p95}ms > ${MAX_P95_MS}ms"
        return 1
    fi
    baseline=""
    if ((fw_n >= MIN_BACKEND_REQUESTS)); then
        baseline="$fw_p95"
    else
        baseline="$(state_get baseline_fw_p95)"
    fi
    if [[ -n "$baseline" ]] && ((core_n >= MIN_BACKEND_REQUESTS)); then
        limit="$(awk -v b="$(fmax "$baseline" "$P95_RATIO_FLOOR_MS")" -v r="$MAX_P95_RATIO" 'BEGIN { printf "%d\n", b * r }')"
        if fgt "$core_p95" "$limit"; then
            GATE_REASON=".NET Core p95 ${core_p95}ms > ${MAX_P95_RATIO}x framework baseline (${baseline}ms, limit ${limit}ms)"
            return 1
        fi
    fi
    if ((fw_n >= MIN_BACKEND_REQUESTS)) && fgt "$fw_err" "$MAX_5XX_PCT"; then
        log "WARNING: .NET Framework 5xx rate ${fw_err}% also exceeds ${MAX_5XX_PCT}% - suspect a shared dependency (database, storage)"
    fi
    # nginx marks a failing peer down (max_fails/fail_timeout) and silently sends its clients to the
    # other backend, so a .NET Core outage shows up as a missing share rather than as errors.
    if fgt "$(awk -v a="$core_share" -v b="$stage" 'BEGIN { print b - a }')" "$SPLIT_TOLERANCE_PCT"; then
        GATE_REASON=".NET Core received ${core_share}% of requests, more than ${SPLIT_TOLERANCE_PCT} points below ${stage}% (peer marked down, or config not applied)"
        return 1
    fi
    if fgt "$(awk -v a="$core_share" -v b="$stage" 'BEGIN { print a - b }')" "$SPLIT_TOLERANCE_PCT"; then
        log "WARNING: .NET Core received ${core_share}%, more than ${SPLIT_TOLERANCE_PCT} points above ${stage}% (few distinct clients, or the framework app is failing over)"
    fi
    return 0
}

# One synthetic request per probe path through nginx. Returns 1 if any failed.
probe_once() {
    local path res code rc=0
    for path in $CANARY_PROBE_PATHS; do
        res="$(http_probe -H "X-Forwarded-For: $(rand_client_ip)" -H "User-Agent: eshop-canary-probe" "$CANARY_BASE_URL$path")"
        code="${res%% *}"
        if ! [[ "$code" =~ ^[23][0-9][0-9]$ ]]; then
            log "probe $path -> HTTP $code"
            rc=1
        fi
    done
    return "$rc"
}

# Probes and evaluates the gates at STAGE for DURATION seconds (0 = forever).
# Returns 0 when healthy for the whole period, 1 on a gate breach (GATE_REASON set).
observe() {
    local stage=$1 duration=$2 start end next_check consecutive=0 rc
    start=$(date +%s)
    end=$((start + duration))
    next_check=$((start + CANARY_CHECK_INTERVAL))
    log "observing stage $stage $([[ "$duration" == 0 ]] && echo 'until interrupted' || echo "for ${duration}s") (check every ${CANARY_CHECK_INTERVAL}s, window ${CANARY_WINDOW_SECONDS}s)"
    while [[ "$duration" == 0 ]] || (($(date +%s) < end)); do
        if probe_once; then
            consecutive=0
        else
            consecutive=$((consecutive + 1))
            if ((consecutive >= MAX_CONSECUTIVE_PROBE_FAILURES)); then
                GATE_REASON="$consecutive consecutive synthetic probe failures"
                return 1
            fi
        fi
        if (($(date +%s) >= next_check)); then
            next_check=$(($(date +%s) + CANARY_CHECK_INTERVAL))
            rc=0
            evaluate_gates "$stage" || rc=$?
            case $rc in
                0) log "gates ok   t=$(($(date +%s) - start))s $GATE_SUMMARY" ;;
                1) return 1 ;;
                2) log "gates wait t=$(($(date +%s) - start))s $GATE_REASON" ;;
            esac
        fi
        sleep "$CANARY_PROBE_INTERVAL"
    done
    rc=0
    evaluate_gates "$stage" || rc=$?
    case $rc in
        0) log "gates ok   final $GATE_SUMMARY" ;;
        1) return 1 ;;
        2)
            if [[ "$CANARY_REQUIRE_TRAFFIC" == 1 ]]; then
                GATE_REASON="inconclusive: $GATE_REASON"
                return 1
            fi
            log "WARNING: gates inconclusive: $GATE_REASON"
            ;;
    esac
    return 0
}

# p95 latency (ms) of COUNT direct requests to URL; empty if any request failed.
direct_p95() {
    local url=$1 count=$2 i res
    for ((i = 0; i < count; i++)); do
        res="$(http_probe "$url")"
        [[ "${res%% *}" =~ ^[23][0-9][0-9]$ ]] || return 0
        awk -v t="${res#* }" 'BEGIN { printf "%d\n", t * 1000 + 0.5 }'
    done | sort -n | awk '{ v[NR] = $1 } END { if (NR == 0) exit; i = int(NR * 0.95); if (i < NR * 0.95) i++; print v[i] }'
}

capture_baseline() {
    local stage=$1 total fw_n fw_p95 rest p95
    if is_stage "$stage"; then
        read -r total _ _ _ fw_n _ fw_p95 rest <<<"$(analyze_window "$(window_since "$CANARY_WINDOW_SECONDS")" "$stage")"
        : "$total" "$rest"
        if ((fw_n >= MIN_BACKEND_REQUESTS)); then
            state_set baseline_fw_p95 "$fw_p95"
            log "latency baseline: .NET Framework p95 ${fw_p95}ms over $fw_n proxied requests at stage $stage"
            return
        fi
    fi
    p95="$(direct_p95 "$FRAMEWORK_URL$CANARY_CATALOG_PATH" 20)"
    if [[ -n "$p95" ]]; then
        state_set baseline_fw_p95 "$p95"
        log "latency baseline: .NET Framework p95 ${p95}ms over 20 direct requests"
    else
        log "WARNING: could not measure a .NET Framework latency baseline; keeping '$(state_get baseline_fw_p95)'"
    fi
}

check() {
    local label=$1
    shift
    if "$@"; then
        printf '  [PASS] %s\n' "$label" >&2
        return 0
    fi
    printf '  [FAIL] %s\n' "$label" >&2
    return 1
}

core_detailed_healthy() {
    curl -sS --max-time "$CANARY_HTTP_TIMEOUT" "$CORE_URL/api/health/detailed" 2>/dev/null |
        tr -d '[:space:]' | grep -q '"database":{"status":"Healthy"'
}

stage_file_ok() { verify_stage_file "$1" >/dev/null; }

current_gates_ok() {
    local stage=$1 rc=0
    evaluate_gates "$stage" || rc=$?
    if ((rc == 1)); then
        echo "    $GATE_REASON ($GATE_SUMMARY)" >&2
        return 1
    fi
    return 0
}

backends_ready_for() {
    local target=$1 ok=0
    if ((target > 0)); then
        check ".NET Core health $CORE_URL/api/health" http_ok "$CORE_URL/api/health" || ok=1
        check ".NET Core catalog $CORE_URL$CANARY_CATALOG_PATH" http_ok "$CORE_URL$CANARY_CATALOG_PATH" || ok=1
    fi
    if ((target < 100)); then
        check ".NET Framework health $FRAMEWORK_URL$FRAMEWORK_HEALTH_PATH" http_ok "$FRAMEWORK_URL$FRAMEWORK_HEALTH_PATH" || ok=1
        check ".NET Framework catalog $FRAMEWORK_URL$CANARY_CATALOG_PATH" http_ok "$FRAMEWORK_URL$CANARY_CATALOG_PATH" || ok=1
    fi
    return "$ok"
}

preflight() {
    local cur=$1 target=$2 ok=0
    log "preflight for stage $cur -> $target"
    check "stage file $(stage_file "$target")" stage_file_ok "$target" || ok=1
    backends_ready_for "$target" || ok=1
    if ((target > 0)); then
        check ".NET Core detailed health reports database Healthy" core_detailed_healthy || ok=1
    fi
    # The previous stage must stay healthy: it is the automatic rollback target.
    if is_stage "$cur" && ((target > 0)); then
        backends_ready_for "$cur" >/dev/null 2>&1 || check "rollback target (stage $cur) backends healthy" backends_ready_for "$cur" || ok=1
    fi
    if is_stage "$cur"; then
        check "proxy $CANARY_BASE_URL/api/health" http_ok "$CANARY_BASE_URL/api/health" || ok=1
        check "no gate breach at current stage $cur" current_gates_ok "$cur" || ok=1
    fi
    return "$ok"
}

show_diff() {
    local target=$1
    if [[ -f "$NGINX_CONF_TARGET" ]]; then
        diff -u "$NGINX_CONF_TARGET" "$(stage_file "$target")" || true
    else
        echo "(no file at $NGINX_CONF_TARGET yet; $(stage_file "$target") will be installed)"
    fi
}

next_stage() {
    local idx
    idx="$(stage_index "$1")"
    echo "${STAGES[$((idx + 1))]}"
}

prev_stage() {
    local idx
    idx="$(stage_index "$1")"
    echo "${STAGES[$((idx - 1))]}"
}

cmd_advance() {
    local cur target soak auto
    ensure_state_dir
    acquire_lock
    cur="$(detect_stage "$NGINX_CONF_TARGET")"
    if [[ -n "$OPT_TO" ]]; then
        target="$OPT_TO"
    else
        case "$cur" in
            none | unknown) fail "$E_USAGE" "no managed stage installed at $NGINX_CONF_TARGET ($cur); bootstrap with: $0 advance --to 0" ;;
            100)
                log "already at stage 100; nothing to advance"
                return 0
                ;;
        esac
        target="$(next_stage "$cur")"
    fi
    is_stage "$target" || fail "$E_USAGE" "invalid stage '$target' (valid: ${STAGES[*]})"
    if is_stage "$cur"; then
        ((target > cur)) || fail "$E_USAGE" "stage $target is not ahead of the current stage $cur; use '$0 rollback --to $target'"
        if [[ "$target" != "$(next_stage "$cur")" && "$OPT_FORCE" != 1 ]]; then
            fail "$E_USAGE" "stage $cur -> $target skips $(next_stage "$cur"); advance one stage at a time or pass --force"
        fi
    elif [[ "$target" != 0 && "$OPT_FORCE" != 1 ]]; then
        fail "$E_USAGE" "current config is '$cur'; bootstrap with --to 0 or pass --force"
    fi

    soak="${OPT_SOAK:-$CANARY_SOAK_SECONDS}"
    auto="${OPT_AUTO_ROLLBACK:-1}"
    echo "Plan: stage $cur -> $target (${target}% of $CANARY_CATALOG_PATH to .NET Core), soak ${soak}s, auto-rollback $([[ "$auto" == 1 ]] && echo on || echo off)" >&2
    if [[ "$OPT_DRY_RUN" == 1 ]]; then
        show_diff "$target"
        return 0
    fi
    preflight "$cur" "$target" || {
        history_add advance "$cur" "$target" preflight-failed "$OPT_REASON"
        fail "$E_PREFLIGHT" "preflight failed; nothing was changed"
    }
    confirm "Advance $CANARY_CATALOG_PATH from stage $cur to stage $target?"

    capture_baseline "$cur"
    if ! activate_stage "$target" "advance $cur -> $target"; then
        history_add advance "$cur" "$target" config-failed "$OPT_REASON"
        fail "$E_CONFIG" "nginx rejected stage $target; previous config restored"
    fi
    history_add advance "$cur" "$target" applied "$OPT_REASON"

    if ! verify_live "$target"; then
        auto_rollback "$cur" "stage $target not observed live"
        exit "$E_VERIFY"
    fi
    if ((target == 0)); then
        history_add advance "$cur" "$target" ok "$OPT_REASON"
        log "stage 0 installed: all $CANARY_CATALOG_PATH traffic on .NET Framework. Next: $0 advance"
        return 0
    fi
    if ! observe "$target" "$soak"; then
        log "GATE BREACH at stage $target: $GATE_REASON"
        log "  $GATE_SUMMARY"
        history_add advance "$cur" "$target" "gate-breach" "$GATE_REASON"
        if [[ "$auto" == 1 ]]; then
            auto_rollback "$cur" "$GATE_REASON"
        else
            log "auto-rollback disabled; stage $target left in place. Roll back with: $0 rollback"
        fi
        exit "$E_GATE"
    fi
    history_add advance "$cur" "$target" ok "$OPT_REASON"
    if ((target == 100)); then
        log "stage 100 healthy: all $CANARY_CATALOG_PATH traffic on .NET Core. Keep '$0 watch' running through the bake period (see runbook)."
    else
        log "stage $target healthy after ${soak}s. Complete the manual checks in the runbook, then: $0 advance"
    fi
}

auto_rollback() {
    local to=$1 why=$2
    log "ROLLING BACK to $([[ "$to" == none || "$to" == unknown ]] && echo 'the previous config' || echo "stage $to"): $why"
    if is_stage "$to"; then
        if activate_stage "$to" "auto-rollback: $why" && verify_live "$to"; then
            history_add rollback "$(state_get previous_stage)" "$to" ok "auto: $why"
            log "rolled back to stage $to"
            return 0
        fi
    else
        local failed_backup=$LAST_BACKUP
        restore_previous "$failed_backup"
        if run_nginx_cmd "nginx config test" "$NGINX_TEST_CMD" && run_nginx_cmd "nginx reload" "$NGINX_RELOAD_CMD"; then
            history_add rollback - "${failed_backup:-none}" ok "auto: $why"
            return 0
        fi
    fi
    history_add rollback - "$to" failed "auto: $why"
    log "AUTOMATIC ROLLBACK FAILED - follow the manual rollback section of the runbook NOW"
    return 1
}

cmd_rollback() {
    local cur target duration
    ensure_state_dir
    acquire_lock
    cur="$(detect_stage "$NGINX_CONF_TARGET")"
    if [[ -n "$OPT_TO" ]]; then
        target="$OPT_TO"
    else
        is_stage "$cur" || fail "$E_USAGE" "current config is '$cur'; pass --to N or use '$0 restore --file <backup>'"
        if ((cur == 0)); then
            log "already at stage 0; nothing to roll back"
            return 0
        fi
        target="$(prev_stage "$cur")"
    fi
    is_stage "$target" || fail "$E_USAGE" "invalid stage '$target' (valid: ${STAGES[*]})"
    if is_stage "$cur" && ((target >= cur)) && [[ "$OPT_FORCE" != 1 ]]; then
        fail "$E_USAGE" "stage $target is not behind the current stage $cur; use '$0 advance'"
    fi
    echo "Plan: ROLLBACK stage $cur -> $target (${target}% of $CANARY_CATALOG_PATH to .NET Core)" >&2
    if [[ "$OPT_DRY_RUN" == 1 ]]; then
        show_diff "$target"
        return 0
    fi
    if ! backends_ready_for "$target"; then
        if [[ "$OPT_FORCE" != 1 ]]; then
            history_add rollback "$cur" "$target" preflight-failed "$OPT_REASON"
            fail "$E_PREFLIGHT" "the backend(s) stage $target sends traffic to are unhealthy; pass --force to roll back anyway"
        fi
        log "WARNING: rolling back onto unhealthy backend(s) because of --force"
    fi
    if ! activate_stage "$target" "rollback $cur -> $target"; then
        history_add rollback "$cur" "$target" config-failed "$OPT_REASON"
        fail "$E_CONFIG" "nginx rejected stage $target; previous config restored - see the manual rollback section of the runbook"
    fi
    if ! verify_live "$target"; then
        history_add rollback "$cur" "$target" verify-failed "$OPT_REASON"
        fail "$E_VERIFY" "stage $target installed but not observed live; check nginx (see runbook troubleshooting)"
    fi
    history_add rollback "$cur" "$target" ok "$OPT_REASON"
    log "rolled back to stage $target"
    duration="${OPT_DURATION:-$CANARY_ROLLBACK_WATCH_SECONDS}"
    if ((target > 0 && duration > 0)); then
        observe "$target" "$duration" || log "WARNING: gate still failing at stage $target: $GATE_REASON - consider '$0 rollback'"
    elif ((duration > 0)); then
        log "post-rollback check: $(for _ in 1 2 3; do probe_once && echo -n ok || echo -n X; done)"
    fi
}

cmd_restore() {
    local stage
    [[ -n "$OPT_FILE" && -f "$OPT_FILE" ]] || fail "$E_USAGE" "restore needs --file <existing backup>"
    ensure_state_dir
    acquire_lock
    if [[ "$OPT_DRY_RUN" == 1 ]]; then
        diff -u "$NGINX_CONF_TARGET" "$OPT_FILE" || true
        return 0
    fi
    confirm "Install $OPT_FILE as $NGINX_CONF_TARGET?"
    activate_file "$OPT_FILE" "restore $OPT_FILE" || fail "$E_CONFIG" "nginx rejected $OPT_FILE; previous config restored"
    stage="$(marker_stage "$OPT_FILE")"
    history_add restore - "$(basename "$OPT_FILE")" ok "$OPT_REASON"
    if [[ -n "$stage" ]]; then
        verify_live "$stage" || fail "$E_VERIFY" "restored config not observed live"
    fi
    log "restored $OPT_FILE"
}

print_report() {
    local stage=$1 window=$2 since
    local total core_n core_5xx core_p95 fw_n fw_5xx fw_p95 other_n other_5xx failovers core_p50 fw_p50
    since="$(window_since "$window")"
    read -r total core_n core_5xx core_p95 fw_n fw_5xx fw_p95 other_n other_5xx failovers core_p50 fw_p50 \
        <<<"$(analyze_window "$since" "$stage")"
    echo "Traffic on $CANARY_CATALOG_PATH at stage $stage since $(date -u -d "@${since%.*}" +%FT%TZ 2>/dev/null || echo "$since") (log: $CANARY_ACCESS_LOG)"
    printf '  %-12s %9s %8s %6s %7s %7s %7s\n' backend requests share 5xx 5xx% p50ms p95ms
    printf '  %-12s %9d %7s%% %6d %7s %7d %7d\n' ".NET Core" "$core_n" "$(pct_of "$core_n" "$total")" "$core_5xx" "$(pct_of "$core_5xx" "$core_n")" "$core_p50" "$core_p95"
    printf '  %-12s %9d %7s%% %6d %7s %7d %7d\n' ".NET Fwk" "$fw_n" "$(pct_of "$fw_n" "$total")" "$fw_5xx" "$(pct_of "$fw_5xx" "$fw_n")" "$fw_p50" "$fw_p95"
    printf '  %-12s %9d %7s%% %6d\n' "no upstream" "$other_n" "$(pct_of "$other_n" "$total")" "$other_5xx"
    echo "  total $total, failovers $failovers ($(pct_of "$failovers" "$total")%), expected .NET Core share ${stage}%, framework baseline p95 $(state_get baseline_fw_p95)ms"
}

cmd_status() {
    local cur rc=0
    cur="$(detect_stage "$NGINX_CONF_TARGET")"
    echo "Installed config : $NGINX_CONF_TARGET"
    echo "Installed stage  : $cur"
    echo "Live header      : X-Canary-Stage=$(live_stage_header || true)"
    echo "Switched at      : $(state_get switched_at) (previous stage: $(state_get previous_stage))"
    echo "Backends:"
    check ".NET Core $CORE_URL/api/health" http_ok "$CORE_URL/api/health" 2>&1 || true
    check ".NET Core detailed health (database)" core_detailed_healthy 2>&1 || true
    check ".NET Framework $FRAMEWORK_URL$FRAMEWORK_HEALTH_PATH" http_ok "$FRAMEWORK_URL$FRAMEWORK_HEALTH_PATH" 2>&1 || true
    if is_stage "$cur"; then
        print_report "$cur" "$CANARY_WINDOW_SECONDS"
        evaluate_gates "$cur" || rc=$?
        case $rc in
            0) echo "Gates            : ok" ;;
            1) echo "Gates            : BREACH - $GATE_REASON" ;;
            2) echo "Gates            : not enough data - $GATE_REASON" ;;
        esac
    fi
    echo "Recent history:"
    if [[ -f "$CANARY_STATE_DIR/history.tsv" ]]; then
        tail -n 5 "$CANARY_STATE_DIR/history.tsv" | sed 's/^/  /'
    else
        echo "  (none)"
    fi
}

cmd_watch() {
    local cur duration
    cur="$(detect_stage "$NGINX_CONF_TARGET")"
    is_stage "$cur" || fail "$E_USAGE" "no managed stage installed at $NGINX_CONF_TARGET ($cur)"
    duration="${OPT_DURATION:-0}"
    if observe "$cur" "$duration"; then
        return 0
    fi
    log "GATE BREACH at stage $cur: $GATE_REASON"
    log "  $GATE_SUMMARY"
    if [[ "$OPT_AUTO_ROLLBACK" == 1 && "$cur" != 0 ]]; then
        ensure_state_dir
        acquire_lock
        history_add watch "$cur" "$cur" gate-breach "$GATE_REASON"
        auto_rollback "$(prev_stage "$cur")" "$GATE_REASON" || true
    else
        log "roll back with: $0 rollback"
    fi
    exit "$E_GATE"
}

cmd_report() {
    local cur
    cur="$(detect_stage "$NGINX_CONF_TARGET")"
    [[ -n "$OPT_TO" ]] && cur="$OPT_TO"
    is_stage "$cur" || fail "$E_USAGE" "no managed stage installed at $NGINX_CONF_TARGET ($cur); pass --to N"
    print_report "$cur" "${OPT_WINDOW:-$CANARY_WINDOW_SECONDS}"
}

cmd_verify_configs() {
    local s ok=0
    for s in "${STAGES[@]}"; do
        verify_stage_file "$s" || ok=1
    done
    return "$ok"
}

cmd_preflight() {
    local cur target
    cur="$(detect_stage "$NGINX_CONF_TARGET")"
    if [[ -n "$OPT_TO" ]]; then
        target="$OPT_TO"
    elif is_stage "$cur" && ((cur < 100)); then
        target="$(next_stage "$cur")"
    else
        target=0
    fi
    is_stage "$target" || fail "$E_USAGE" "invalid stage '$target'"
    preflight "$cur" "$target" || fail "$E_PREFLIGHT" "preflight failed"
    log "preflight passed for stage $cur -> $target"
}

cmd_history() {
    if [[ -f "$CANARY_STATE_DIR/history.tsv" ]]; then
        column -t -s "$(printf '\t')" "$CANARY_STATE_DIR/history.tsv" 2>/dev/null || cat "$CANARY_STATE_DIR/history.tsv"
    else
        echo "(no history in $CANARY_STATE_DIR)"
    fi
}

parse_opts() {
    while (($#)); do
        case "$1" in
            --to) OPT_TO="${2:?--to needs a stage}"; shift ;;
            --yes | -y) OPT_YES=1 ;;
            --force) OPT_FORCE=1 ;;
            --dry-run) OPT_DRY_RUN=1 ;;
            --soak) OPT_SOAK="${2:?--soak needs seconds}"; shift ;;
            --no-auto-rollback) OPT_AUTO_ROLLBACK=0 ;;
            --auto-rollback) OPT_AUTO_ROLLBACK=1 ;;
            --duration) OPT_DURATION="${2:?--duration needs seconds}"; shift ;;
            --window) OPT_WINDOW="${2:?--window needs seconds}"; shift ;;
            --reason) OPT_REASON="${2:?--reason needs text}"; shift ;;
            --file) OPT_FILE="${2:?--file needs a path}"; shift ;;
            -h | --help) usage; exit 0 ;;
            *) usage >&2; fail "$E_USAGE" "unknown option '$1'" ;;
        esac
        shift
    done
    local n
    for n in "$OPT_SOAK" "$OPT_DURATION" "$OPT_WINDOW"; do
        [[ -z "$n" || "$n" =~ ^[0-9]+$ ]] || fail "$E_USAGE" "'$n' is not a number of seconds"
    done
}

main() {
    local cmd=${1:-}
    [[ -n "$cmd" ]] || { usage >&2; exit "$E_USAGE"; }
    shift
    parse_opts "$@"
    case "$cmd" in
        status) cmd_status ;;
        preflight) cmd_preflight ;;
        advance) cmd_advance ;;
        rollback) cmd_rollback ;;
        restore) cmd_restore ;;
        watch) cmd_watch ;;
        report) cmd_report ;;
        verify-configs) cmd_verify_configs ;;
        history) cmd_history ;;
        -h | --help | help) usage ;;
        *) usage >&2; fail "$E_USAGE" "unknown command '$cmd'" ;;
    esac
}

main "$@"
