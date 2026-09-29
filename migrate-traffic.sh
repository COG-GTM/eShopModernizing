#!/usr/bin/env bash
# Deprecated entry point kept for existing muscle memory and automation.
# All stage changes go through canary/cutover.sh - see CANARY-CUTOVER-RUNBOOK.md.
set -euo pipefail

CUTOVER="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/canary/cutover.sh"

case "${1:-}" in
    0 | 25 | 50 | 75 | 100)
        stage=$1
        shift
        echo "migrate-traffic.sh is deprecated; running: canary/cutover.sh advance --to $stage $*" >&2
        exec "$CUTOVER" advance --to "$stage" "$@"
        ;;
    rollback)
        shift
        exec "$CUTOVER" rollback "$@"
        ;;
    *)
        echo "Usage: $0 {0|25|50|75|100} [cutover options]   (advance to that stage)" >&2
        echo "       $0 rollback [--to N]                     (roll back)" >&2
        echo "Prefer canary/cutover.sh directly - see CANARY-CUTOVER-RUNBOOK.md." >&2
        exit 1
        ;;
esac
