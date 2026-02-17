#!/bin/bash


NGINX_CONFIG_DIR="/etc/nginx"
BACKUP_DIR="./nginx-backups"
LOG_FILE="./migration.log"

log() {
    echo "$(date): $1" | tee -a $LOG_FILE
}

backup_config() {
    mkdir -p $BACKUP_DIR
    cp nginx.conf "$BACKUP_DIR/nginx-$(date +%s).conf"
    log "Backed up current nginx configuration"
}

apply_config() {
    local config_file=$1
    local percentage=$2
    
    log "Applying $percentage traffic to .NET Core"
    cp $config_file nginx.conf
    
    
    log "Configuration applied: $config_file"
}

monitor_health() {
    local duration=$1
    local error_threshold=${2:-15}
    local error_count=0
    log "Monitoring health for $duration seconds (error threshold: $error_threshold)"
    
    for i in $(seq 1 $duration); do
        curl -s http://localhost/api/health > /dev/null
        if [ $? -eq 0 ]; then
            echo -n "."
            error_count=0
        else
            echo -n "X"
            error_count=$((error_count + 1))
            if [ $error_count -ge $error_threshold ]; then
                echo ""
                log "ERROR: Health check failed $error_threshold consecutive times. Consider rollback."
                return 1
            fi
        fi
        sleep 1
    done
    echo ""
    return 0
}

print_usage() {
    echo "Usage: $0 {25|50|75|100|phase4-25|phase4-50|phase4-75|phase4-100}"
    echo ""
    echo "Catalog Migration (Phase 3):"
    echo "  25   - Route 25% of catalog traffic to .NET Core"
    echo "  50   - Route 50% of catalog traffic to .NET Core"
    echo "  75   - Route 75% of catalog traffic to .NET Core"
    echo "  100  - Route 100% of catalog traffic to .NET Core"
    echo ""
    echo "Remaining Routes Migration (Phase 4):"
    echo "  phase4-25   - Route 25% of remaining traffic to .NET Core"
    echo "  phase4-50   - Route 50% of remaining traffic to .NET Core"
    echo "  phase4-75   - Route 75% of remaining traffic to .NET Core"
    echo "  phase4-100  - Route 100% of all traffic to .NET Core"
    echo ""
    echo "Phase 4 migrates: Account, Home, uploadimage, static files, and all catch-all routes"
}

case $1 in
    "25")
        backup_config
        apply_config "nginx-25percent.conf" "25%"
        monitor_health 300
        ;;
    "50")
        backup_config
        apply_config "nginx-50percent.conf" "50%"
        monitor_health 300
        ;;
    "75")
        backup_config
        apply_config "nginx-75percent.conf" "75%"
        monitor_health 300
        ;;
    "100")
        backup_config
        apply_config "nginx-100percent.conf" "100%"
        monitor_health 300
        ;;
    "phase4-25")
        backup_config
        log "Phase 4: Starting remaining routes migration at 25%"
        log "Routes being migrated: /Account/*, /Home/*, /uploadimage, static files, catch-all /"
        apply_config "phase4-nginx-25percent.conf" "Phase4-25%"
        monitor_health 300
        ;;
    "phase4-50")
        backup_config
        log "Phase 4: Increasing remaining routes migration to 50%"
        apply_config "phase4-nginx-50percent.conf" "Phase4-50%"
        monitor_health 300
        ;;
    "phase4-75")
        backup_config
        log "Phase 4: Increasing remaining routes migration to 75%"
        apply_config "phase4-nginx-75percent.conf" "Phase4-75%"
        monitor_health 300
        ;;
    "phase4-100")
        backup_config
        log "Phase 4: Completing remaining routes migration at 100%"
        log "All traffic now routed to .NET Core. Modernized .NET Framework (port 5001) ready for decommissioning."
        apply_config "phase4-nginx-100percent.conf" "Phase4-100%"
        monitor_health 300
        ;;
    *)
        print_usage
        exit 1
        ;;
esac
