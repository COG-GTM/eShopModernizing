#!/bin/bash
#
# Shift catalog traffic between the .NET Framework app (:5001) and the .NET Core app (:5002)
# one canary stage at a time. Each stage is gated on health: the .NET Core backend must be
# ready before traffic moves, and if health checks fail during the watch window the previous
# nginx configuration is restored automatically.
#
# Environment overrides:
#   ACTIVE_CONFIG              nginx config file served by the gateway      (./nginx.conf)
#   NGINX_TEST_CMD             validates the config ("" to skip)            (nginx -t)
#   NGINX_RELOAD_CMD           reloads the gateway ("" to skip)             (nginx -s reload)
#   GATEWAY_URL                gateway base URL                             (http://localhost)
#   CORE_URL                   .NET Core backend base URL                   (http://localhost:5002)
#   MONITOR_DURATION           seconds to watch each stage                  (300)
#   MONITOR_INTERVAL           seconds between probes                       (5)
#   MAX_CONSECUTIVE_FAILURES   failed probes in a row that trigger rollback (3)
#   MAX_FAILURE_PERCENT        failed probe percentage that triggers rollback (10)
#
# When the gateway runs from docker-compose.canary.yml:
#   NGINX_TEST_CMD="docker compose -f docker-compose.canary.yml exec -T gateway nginx -t" \
#   NGINX_RELOAD_CMD="docker compose -f docker-compose.canary.yml exec -T gateway nginx -s reload" \
#   ./migrate-traffic.sh 25

set -uo pipefail

ACTIVE_CONFIG="${ACTIVE_CONFIG:-./nginx.conf}"
BACKUP_DIR="${BACKUP_DIR:-./nginx-backups}"
LOG_FILE="${LOG_FILE:-./migration.log}"
NGINX_TEST_CMD="${NGINX_TEST_CMD-nginx -t}"
NGINX_RELOAD_CMD="${NGINX_RELOAD_CMD-nginx -s reload}"
GATEWAY_URL="${GATEWAY_URL:-http://localhost}"
CORE_URL="${CORE_URL:-http://localhost:5002}"
MONITOR_DURATION="${MONITOR_DURATION:-300}"
MONITOR_INTERVAL="${MONITOR_INTERVAL:-5}"
MAX_CONSECUTIVE_FAILURES="${MAX_CONSECUTIVE_FAILURES:-3}"
MAX_FAILURE_PERCENT="${MAX_FAILURE_PERCENT:-10}"
PROBE_TIMEOUT="${PROBE_TIMEOUT:-5}"

LAST_BACKUP=""

log() {
    local level=$1
    shift
    printf '%s [%s] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$level" "$*" | tee -a "$LOG_FILE" >&2
}

run_hook() {
    local description=$1
    local command=$2
    if [ -z "$command" ]; then
        return 0
    fi
    if ! output=$(bash -c "$command" 2>&1); then
        log ERROR "$description failed: $output"
        return 1
    fi
    return 0
}

# Prints "<http status> <health status>" for a health endpoint; health status is "-" when absent.
probe_health() {
    local url=$1
    local body status health
    body=$(curl -s --max-time "$PROBE_TIMEOUT" -H "X-Correlation-ID: migrate-traffic-$$" -w '\n%{http_code}' "$url" 2>/dev/null)
    status=$(printf '%s' "$body" | tail -n1)
    health=$(printf '%s' "$body" | sed '$d' | grep -o '"status":"[A-Za-z]*"' | head -n1 | cut -d'"' -f4)
    echo "${status:-000} ${health:--}"
}

# A probe passes when HTTP is 2xx and the reported status is Healthy or Degraded.
health_ok() {
    local url=$1
    local result status health
    result=$(probe_health "$url")
    status=${result%% *}
    health=${result#* }
    if [[ "$status" =~ ^2[0-9][0-9]$ ]] && [[ "$health" == "Healthy" || "$health" == "Degraded" ]]; then
        return 0
    fi
    log WARN "Health probe failed: $url -> HTTP $status, status $health"
    return 1
}

http_ok() {
    local url=$1
    local status
    status=$(curl -s -o /dev/null --max-time "$PROBE_TIMEOUT" -H "X-Correlation-ID: migrate-traffic-$$" -w '%{http_code}' "$url" 2>/dev/null)
    if [[ "$status" =~ ^[23][0-9][0-9]$ ]]; then
        return 0
    fi
    log WARN "Synthetic request failed: $url -> HTTP ${status:-000}"
    return 1
}

probe_stage() {
    local ok=0
    health_ok "$CORE_URL/health/ready" || ok=1
    health_ok "$GATEWAY_URL/api/health" || ok=1
    http_ok "$GATEWAY_URL/Catalog/" || ok=1
    return $ok
}

backup_config() {
    mkdir -p "$BACKUP_DIR"
    LAST_BACKUP="$BACKUP_DIR/nginx-$(date +%s).conf"
    cp "$ACTIVE_CONFIG" "$LAST_BACKUP"
    log INFO "Backed up current nginx configuration to $LAST_BACKUP"
}

reload_gateway() {
    run_hook "nginx config test" "$NGINX_TEST_CMD" && run_hook "nginx reload" "$NGINX_RELOAD_CMD"
}

restore_config() {
    local backup=$1
    if [ -z "$backup" ] || [ ! -f "$backup" ]; then
        log ERROR "No backup available to restore"
        return 1
    fi
    local expected_stage
    expected_stage=$(sed -n 's/.*add_header X-Migration-Stage \([0-9]*\).*/\1/p' "$backup" | head -n1)
    cp "$backup" "$ACTIVE_CONFIG"
    if reload_gateway && { [ -z "$expected_stage" ] || wait_for_stage "$expected_stage"; }; then
        log INFO "Restored nginx configuration from $backup"
        return 0
    fi
    log ERROR "Restored $backup but the gateway reload failed; manual intervention required"
    return 1
}

gateway_stage() {
    curl -s -o /dev/null -D - --max-time "$PROBE_TIMEOUT" "$GATEWAY_URL/gateway/health" 2>/dev/null \
        | tr -d '\r' | awk -F': ' 'tolower($1) == "x-migration-stage" { print $2 }'
}

wait_for_stage() {
    local expected=$1
    local actual=""
    for _ in $(seq 1 10); do
        actual=$(gateway_stage)
        if [ "$actual" == "$expected" ]; then
            return 0
        fi
        sleep 1
    done
    log ERROR "Gateway reports migration stage '${actual:-none}', expected '$expected'"
    return 1
}

apply_config() {
    local config_file=$1
    local percentage=$2

    log INFO "Applying $percentage traffic to .NET Core ($config_file)"
    cp "$config_file" "$ACTIVE_CONFIG"
    if ! reload_gateway || ! wait_for_stage "${percentage%\%}"; then
        log ERROR "Gateway did not pick up $config_file; rolling back"
        restore_config "$LAST_BACKUP"
        return 1
    fi
    log INFO "Configuration applied: $config_file (gateway serving stage $percentage)"
}

preflight() {
    log INFO "Pre-flight: checking .NET Core readiness at $CORE_URL/health/ready"
    if ! health_ok "$CORE_URL/health/ready"; then
        log ERROR "Pre-flight failed: .NET Core backend is not ready; traffic was not shifted"
        return 1
    fi
}

monitor_health() {
    local duration=$1
    local deadline=$((SECONDS + duration))
    local total=0 failed=0 consecutive=0

    log INFO "Monitoring health for ${duration}s (interval ${MONITOR_INTERVAL}s, rollback after $MAX_CONSECUTIVE_FAILURES consecutive failures or >${MAX_FAILURE_PERCENT}% failed probes)"
    while [ "$SECONDS" -lt "$deadline" ]; do
        total=$((total + 1))
        if probe_stage; then
            consecutive=0
            echo -n "." >&2
        else
            failed=$((failed + 1))
            consecutive=$((consecutive + 1))
            echo -n "X" >&2
            if [ "$consecutive" -ge "$MAX_CONSECUTIVE_FAILURES" ]; then
                echo "" >&2
                log ERROR "$consecutive consecutive failed probes ($failed/$total total)"
                return 1
            fi
        fi
        sleep "$MONITOR_INTERVAL"
    done
    echo "" >&2

    if [ $((failed * 100)) -gt $((total * MAX_FAILURE_PERCENT)) ]; then
        log ERROR "Failure rate too high: $failed/$total probes failed"
        return 1
    fi
    log INFO "Stage healthy: $failed/$total probes failed"
}

run_stage() {
    local config_file=$1
    local percentage=$2

    preflight || exit 2
    backup_config
    apply_config "$config_file" "$percentage" || exit 3
    if ! monitor_health "$MONITOR_DURATION"; then
        log ERROR "Stage $percentage unhealthy; rolling back"
        restore_config "$LAST_BACKUP"
        exit 4
    fi
    log INFO "Stage $percentage complete"
}

case "${1:-}" in
    "25")  run_stage "nginx-25percent.conf" "25%" ;;
    "50")  run_stage "nginx-50percent.conf" "50%" ;;
    "75")  run_stage "nginx-75percent.conf" "75%" ;;
    "100") run_stage "nginx-100percent.conf" "100%" ;;
    "rollback")
        latest=$(ls -1t "$BACKUP_DIR"/nginx-*.conf 2>/dev/null | head -n1)
        restore_config "$latest" || exit 1
        ;;
    "status")
        echo "gateway stage:  $(gateway_stage)"
        echo "core ready:     $(probe_health "$CORE_URL/health/ready")"
        echo "gateway health: $(probe_health "$GATEWAY_URL/api/health")"
        ;;
    *)
        echo "Usage: $0 {25|50|75|100|rollback|status}"
        exit 1
        ;;
esac
