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
    log "Monitoring health for $duration seconds"
    
    for i in $(seq 1 $duration); do
        curl -s http://localhost/health > /dev/null
        if [ $? -eq 0 ]; then
            echo -n "."
        else
            echo -n "X"
        fi
        sleep 1
    done
    echo ""
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
    *)
        echo "Usage: $0 {25|50|75|100}"
        exit 1
        ;;
esac
