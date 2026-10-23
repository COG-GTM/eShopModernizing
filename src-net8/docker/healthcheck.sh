#!/usr/bin/env bash
# Container HEALTHCHECK for the eShopNet8 apps: GET /health on the Kestrel port, healthy only on HTTP 200.
# The Debian aspnet:8.0 runtime image ships without curl/wget, so this speaks HTTP over bash's /dev/tcp.
set -euo pipefail
ports="${ASPNETCORE_HTTP_PORTS:-8080}"
port="${ports%%[;,]*}"
exec 3<>"/dev/tcp/127.0.0.1/${port}"
printf 'GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3
IFS= read -r -t 10 status <&3
[[ "${status}" == HTTP/1.?" 200 "* ]]
