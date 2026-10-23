#!/bin/bash
# Container healthcheck without curl/wget (not in the aspnet:8.0 image): HTTP/1.0 GET over bash /dev/tcp.
#   healthcheck [path=/health] [port=8080]   exit 0 on HTTP 200
set -u
path="${1:-/health}"; port="${2:-8080}"
exec 3<>"/dev/tcp/127.0.0.1/$port" || exit 1
printf 'GET %s HTTP/1.0\r\nHost: localhost\r\n\r\n' "$path" >&3
read -r -t 5 _ status _ <&3 || exit 1
[ "$status" = 200 ]
