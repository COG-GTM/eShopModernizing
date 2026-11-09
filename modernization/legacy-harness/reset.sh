#!/usr/bin/env bash
# Resets legacy state. Mock mode keeps the catalog in an in-memory singleton, so a restart resets it.
# In sql mode the catalog database is dropped first so the EF6 initializer re-seeds it on start.
# Usage: reset.sh [all|mvc|webforms]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
WHICH="${1:-all}"
"$HARNESS_DIR/stop.sh" "$WHICH"
if [ "$LEGACY_MODE" = "sql" ]; then "$HARNESS_DIR/sql.sh" reset-db "$WHICH"; fi
"$HARNESS_DIR/start.sh" "$WHICH"
