# Canary Cutover Runbook: `/Catalog/` from .NET Framework to .NET Core

This runbook moves `/Catalog/` traffic from the modernized .NET Framework app (port 5001) to the
.NET Core app (port 5002) in five nginx stages, and describes what to watch at each stage and how
to roll back. All stage changes go through one script: [`canary/cutover.sh`](canary/cutover.sh).

| Stage | File | `/Catalog/` to .NET Core | `/Catalog/` to .NET Framework |
|------:|------|-------------------------:|------------------------------:|
| 0   | `nginx-0percent.conf`   | 0%   | 100% |
| 25  | `nginx-25percent.conf`  | 25%  | 75%  |
| 50  | `nginx-50percent.conf`  | 50%  | 50%  |
| 75  | `nginx-75percent.conf`  | 75%  | 25%  |
| 100 | `nginx-100percent.conf` | 100% | 0%   |

Every stage routes the same way outside `/Catalog/`: `/api/*` (including `/api/health` and
`/api/health/detailed`) goes to .NET Core, and everything else goes to the .NET Framework app.
Only the `catalog_migration` upstream weights and the stage number differ between files.
`canary/tests/test-stage-configs.sh` enforces this.

## Contents

1. [How the stages work](#1-how-the-stages-work)
2. [One-time setup](#2-one-time-setup)
3. [Advancing a stage](#3-advancing-a-stage)
4. [What to watch](#4-what-to-watch)
5. [Rolling back](#5-rolling-back)
6. [Manual (script-free) rollback](#6-manual-script-free-rollback)
7. [Reference: commands, options, exit codes, configuration](#7-reference)
8. [Troubleshooting](#8-troubleshooting)
9. [Changing the stage files](#9-changing-the-stage-files)
10. [Assumptions](#10-assumptions)

---

## 1. How the stages work

- **Weighted, sticky split.** `catalog_migration` uses `hash $canary_client_key consistent`, with
  weights that match the stage percentage. The key is the first `X-Forwarded-For` hop, or the peer
  address when that header is missing. Each client stays on one backend, so auth cookies, session
  state and antiforgery tokens (which the two apps don't share) keep working. Consistent hashing
  also means that clients moved to .NET Core at stage N stay there at stage N+1. Moving forward
  only moves clients *towards* .NET Core. The e2e test checks this.
- **Stage marker.** Each file sets `$canary_stage` and returns it as the `X-Canary-Stage` response
  header, so you can check which stage nginx is serving:
  `curl -sI http://localhost/Catalog/ | grep -i x-canary-stage`.
- **Canary access log.** Each file writes a tab-separated log to `/var/log/nginx/eshop-canary.log`
  (`msec, stage, status, upstream_addr, upstream_status, request_time, upstream_response_time,
  method, uri`). The script reads it to measure the split, error rate, latency and failovers per
  backend. The normal `access.log` is not changed.
- **Passive failover.** nginx's default `proxy_next_upstream error timeout` stays in place. If .NET
  Core refuses a connection, nginx retries the request on the .NET Framework app and marks the
  Core peer down for `fail_timeout`. Users don't see an error, but the Core share drops and failovers
  show up in the log. The gates watch for both.

## 2. One-time setup

On the nginx host, from a checkout of this repository:

1. **Install the stage file where nginx includes it.** The script manages one file:
   `NGINX_CONF_TARGET` (default `/etc/nginx/conf.d/eshop.conf`), included inside `http {}`. Remove
   any other copy of the eShop server block (e.g. an old `nginx.conf` include or
   `sites-enabled/` entry) so that two `server_name localhost` blocks don't conflict.
2. **Create the configuration (optional).** `cp canary/canary.env.example canary/canary.env` and
   edit it. `canary.env` is git-ignored. Every setting can also be set as an environment variable
   (see [7.4](#74-configuration)).
3. **Permissions.** The user running the script needs to write `NGINX_CONF_TARGET`, run `nginx -t`
   and `nginx -s reload`, and read `CANARY_ACCESS_LOG`. In practice you run it with `sudo`. History
   records `SUDO_USER`, so you can still see who made each change.
4. **Check the stage files:**
   ```bash
   canary/cutover.sh verify-configs
   ```
5. **Bootstrap to stage 0.** This installs the all-Framework baseline and adds the stage marker and
   canary log to the live config.
   - Fresh host (nothing at `NGINX_CONF_TARGET`):
     ```bash
     sudo canary/cutover.sh advance --to 0
     ```
   - Host already running the older unmarked `nginx.conf` (weights 3:1): the script detects it
     as stage 25 from the weights. To start again from a clean baseline:
     ```bash
     sudo canary/cutover.sh rollback --to 0 --reason "adopt canary runbook"
     ```
     Or keep the 25% split and let the next `advance` go to 50. Either way, the managed file
     replaces the unmarked one.
6. **Get a baseline.** Leave stage 0 running with normal traffic for at least one window
   (`CANARY_WINDOW_SECONDS`, 5 minutes by default), then run `canary/cutover.sh report`. The
   .NET Framework p95 from this report becomes the latency baseline for the 25% stage.

### Before a cutover window

- [ ] Change ticket or incident reference ready (pass it with `--reason`).
- [ ] Both apps deployed from the intended builds. Both point at the same catalog database.
- [ ] `canary/cutover.sh status` shows both backends `[PASS]` and gates `ok` (or "not enough data"
      at very low traffic).
- [ ] Application Insights dashboards open for both apps (see [4.2](#42-what-to-watch-outside-the-script)).
- [ ] Someone who can run the manual rollback ([6](#6-manual-script-free-rollback)) is on hand.
- [ ] No deployment of either app scheduled during the soak.

## 3. Advancing a stage

```bash
sudo canary/cutover.sh preflight                    # optional: what advance will check, changes nothing
sudo canary/cutover.sh advance --dry-run            # optional: plan + config diff, changes nothing
sudo canary/cutover.sh advance --reason "CHG-1234"  # current stage -> next stage, with soak
```

`advance` moves **one stage** (0 → 25 → 50 → 75 → 100). It stops without changing anything if a
stage would be skipped (unless you pass `--force`). It asks for confirmation (pass `--yes` for
automation; with no terminal and no `--yes` it refuses). An advisory lock (`canary/state/lock`)
stops two operators changing stages at the same time.

What `advance` does, in order:

1. **Preflight** (exit 2 and no change on failure):
   - Target stage file is valid: marker matches the upstream weights.
   - Backends the target stage sends traffic to answer `/api/health` (Core) or
     `FRAMEWORK_HEALTH_PATH` (Framework), and `/Catalog/`, directly on their ports.
   - `.NET Core /api/health/detailed` reports the database `Healthy`.
   - Backends of the **current** stage are healthy, because that stage is the rollback target.
   - The proxy answers `/api/health`, and the gates are not already failing at the current stage.
2. **Baseline.** Records the .NET Framework p95 for `/Catalog/` from the canary log (or from 20
   direct requests if there is too little traffic). The relative latency gate compares against it.
3. **Apply.** Backs up the live file to `canary/state/backups/eshop-<utc>-stage<N>.conf`, installs
   the new stage, runs `nginx -t`, and reloads. If the test or the reload fails, the previous file is
   restored byte-for-byte and the script exits 3.
4. **Verify.** Waits up to `CANARY_VERIFY_TIMEOUT` for three consecutive `X-Canary-Stage: <N>`
   responses. If that doesn't happen, it rolls back and exits 5.
5. **Soak.** For `CANARY_SOAK_SECONDS` (15 minutes by default, or `--soak S`), it sends synthetic
   probes through nginx (`CANARY_PROBE_PATHS`) with randomized client keys, so both backends get
   probed. Every `CANARY_CHECK_INTERVAL` it checks the gates in [4.1](#41-automatic-gates) over the
   last `CANARY_WINDOW_SECONDS`, and logs a line like this:
   ```
   gates ok   t=60s stage=25 req=412 core=24.51% (want 25%) core5xx=0.00% fw5xx=0.00% p95(core/fw)=38/41ms failover=0.00% no-upstream5xx=0/0
   ```
6. **Result.**
   - Healthy: exits 0 and records `applied` in the history.
   - Gate breach: rolls back to the previous stage automatically, records `gate-breach` and the
     rollback, and exits 4. With `--no-auto-rollback` it only reports the breach and leaves the new
     stage in place (use this only while you are watching the dashboards yourself).
   - Not enough traffic by the end of the soak: counts as a breach (`inconclusive`). Set
     `CANARY_REQUIRE_TRAFFIC=0` to only warn, e.g. in a quiet staging environment.

The script's output is also written to `canary/state/cutover.log`.

### Recommended pacing

| Step | Soak (`--soak`) | Before moving on |
|------|-----------------|------------------|
| 0 → 25   | 900 s (default) | Then watch at least **one business day**, including peak traffic, with `canary/cutover.sh watch --auto-rollback` in a `tmux`/`screen` session. |
| 25 → 50  | 1800 s | Check the Application Insights failures/exceptions views for both apps. |
| 50 → 75  | 1800 s | Check image uploads/downloads, authentication and edit/create/delete flows on .NET Core. |
| 75 → 100 | 3600 s | Keep the .NET Framework app running and healthy for at least a week. It is the rollback target. |

These times are suggestions. Soak longer where traffic is low: the gates need at least
`MIN_REQUESTS` requests per window to decide anything.

### Between stages

Between stages, keep a watcher running so a regression that shows up later still triggers a
rollback:

```bash
canary/cutover.sh watch --auto-rollback            # until Ctrl-C; rolls back one stage on breach
canary/cutover.sh watch --duration 600             # report-only for 10 minutes
```

## 4. What to watch

### 4.1 Automatic gates

The script checks these over a sliding window (`CANARY_WINDOW_SECONDS`, default 300 s). It only
counts canary-log entries for `/Catalog/` requests served at the **current** stage, so traffic from
before the change doesn't mix in.

| Gate | Default | Breach means |
|------|---------|--------------|
| Minimum traffic | `MIN_REQUESTS=50` per window | Below this the gates wait. At the end of a soak this counts as "inconclusive" (a breach). |
| .NET Core 5xx rate | `MAX_5XX_PCT=1.0` % | Core is failing requests. Only checked once Core has `MIN_BACKEND_REQUESTS=20`. |
| Upstream failover rate | `MAX_FAILOVER_PCT=1.0` % | nginx had to retry on the other backend (connection refused or timed out). |
| Requests with no upstream | any | Every peer was down. Users got nginx 502/504s. |
| .NET Core p95 (absolute) | `MAX_P95_MS=5000` ms | Core is slow in absolute terms. |
| .NET Core p95 (relative) | `MAX_P95_RATIO=2.0` × max(Framework p95, `P95_RATIO_FLOOR_MS=100`) | Core is much slower than the Framework baseline. The baseline is Framework traffic in the same window, or the one captured before the change. |
| Core share too low | more than `SPLIT_TOLERANCE_PCT=15` points below the stage % | nginx has marked Core down and is quietly sending its clients to the Framework app, or the new config isn't live. |
| Synthetic probes | `MAX_CONSECUTIVE_PROBE_FAILURES=3` | Probes through nginx returned non-2xx/3xx several times in a row. |

Warnings that don't trigger a rollback:
- .NET Framework 5xx rate also above `MAX_5XX_PCT`. Suspect a shared dependency such as the
  database or storage. Rolling back probably won't help. Investigate.
- Core share more than `SPLIT_TOLERANCE_PCT` points *above* the stage %. Usually there are only a
  few distinct clients (e.g. everything behind one corporate NAT or a load balancer that doesn't
  set `X-Forwarded-For`). See [8](#8-troubleshooting).

To see the numbers at any time:

```bash
canary/cutover.sh status                 # stage, live header, backend health, report, gates, history
canary/cutover.sh report --window 900    # split / 5xx / p50 / p95 per backend over 15 minutes
```

### 4.2 What to watch outside the script

The script only sees nginx. During and after every stage, a person should also check:

- **Application Insights, both apps:** failed requests and exceptions per operation, dependency
  failures (SQL, Blob Storage, Key Vault), server response time. Compare .NET Core with the
  .NET Framework app over the same period.
- **Business behaviour on .NET Core:** catalog list/paging, item details, create/edit/delete,
  image upload/download (Azure Storage when `UseAzureStorage` is on), and sign-in when
  `UseAzureActiveDirectory` is on. Run a manual smoke test on a client that is known to land on
  Core: look for `X-Canary-Stage` and use `/api/health/detailed`.
- **Database:** DTU/CPU, deadlocks, and `catalog_hilo` sequence contention. Both apps write to the
  same tables.
- **nginx itself:** `error.log` for `upstream timed out`, `connect() failed` and `no live upstreams`.
- **User signals:** support tickets or complaints about sign-outs or lost form state. This can mean
  affinity is broken, see [8](#8-troubleshooting).

### 4.3 Roll back on these, even if the gates are green

Roll back **one stage** (`sudo canary/cutover.sh rollback`) when any of these happens:

| Signal | Action |
|--------|--------|
| .NET Core 5xx above 1% for 5 minutes (Application Insights or `report`) | Roll back one stage |
| .NET Core p95 above 2× the Framework app for 10 minutes | Roll back one stage |
| Data correctness issue (wrong prices, missing items, bad writes) | **Roll back to 0**, then investigate |
| Authentication failures on .NET Core | **Roll back to 0** |
| Database critical (Core-specific queries) | **Roll back to 0** |
| Azure dependency outage affecting both apps | Don't roll back. Use feature flags (`UseAzureStorage` etc.), see ROLLBACK-PROCEDURES.md |

## 5. Rolling back

```bash
sudo canary/cutover.sh rollback --reason "INC-42 5xx on Core"          # one stage back, then watch 120 s
sudo canary/cutover.sh rollback --to 0 --yes --reason "INC-42"          # emergency: all traffic to Framework
sudo canary/cutover.sh rollback --to 50 --duration 600                  # to a specific stage, watch 10 min
```

- Before switching, `rollback` checks that the backends the target stage uses are healthy. If they
  aren't, it refuses with exit 2, because rolling back onto a dead Framework app makes things worse.
  Override with `--force` only when you are sure, e.g. when the health endpoint itself is broken
  but the app serves traffic.
- After switching, it confirms the stage from the `X-Canary-Stage` header and watches the gates for
  `CANARY_ROLLBACK_WATCH_SECONDS` (120 s by default, or `--duration S`). At stage 0 it runs three
  synthetic probe rounds instead.
- Rollback uses the same backup / `nginx -t` / restore-on-failure path as `advance`.
- Clients moved back to the Framework app lose any .NET Core-only session state and may need to
  sign in again. This is expected.

### Restoring an exact earlier file

Every change backs up the file it replaced (the last `CANARY_BACKUP_KEEP=50` are kept):

```bash
ls -1t canary/state/backups/
sudo canary/cutover.sh restore --file canary/state/backups/eshop-20260929T151200Z-stage50.conf
```

Use `restore` when the live file had been hand-edited, or when it isn't one of the managed stages.

### After any rollback

1. `canary/cutover.sh status`: stage, header, gates.
2. `canary/cutover.sh history`: record what happened.
3. Keep watching at the lower stage for at least 30 minutes (`watch --duration 1800`).
4. Follow "Post-Rollback Actions" and "Rollback Validation" in
   [ROLLBACK-PROCEDURES.md](ROLLBACK-PROCEDURES.md).
5. Don't advance again until the root cause is fixed and deployed.

## 6. Manual (script-free) rollback

Use this if the script itself fails (for example it prints `AUTOMATIC ROLLBACK FAILED`, or the
checkout is unavailable):

```bash
# 1. Put all /Catalog/ traffic back on the .NET Framework app
sudo cp nginx-0percent.conf /etc/nginx/conf.d/eshop.conf   # or the newest file in canary/state/backups/
sudo nginx -t && sudo nginx -s reload

# 2. Confirm
curl -sI http://localhost/Catalog/ | grep -i x-canary-stage    # expect: X-Canary-Stage: 0
curl -s  http://localhost:5001/ -o /dev/null -w '%{http_code}\n'
```

If `nginx -t` fails, restore the newest backup from `canary/state/backups/` the same way. The
script's history doesn't see manual changes, so record the action in the incident/change ticket.
`canary/cutover.sh status` reads the installed stage from the file, so later script commands
continue from the stage you restored.

## 7. Reference

### 7.1 Commands

| Command | Changes nginx | Purpose |
|---------|:-------------:|---------|
| `status` | no | Installed stage, live header, backend health, report, gate result, last 5 history lines |
| `preflight [--to N]` | no | Run the advance checks for the next stage (or stage N) |
| `advance [--to N]` | yes | Next stage (or N), then verify and soak. Auto-rollback on breach |
| `rollback [--to N]` | yes | Previous stage (or N), then verify and watch |
| `restore --file PATH` | yes | Re-install an exact backup |
| `watch [--duration S] [--auto-rollback]` | only with `--auto-rollback` | Probe and check gates at the current stage |
| `report [--window S]` | no | Per-backend split, 5xx, p50/p95, failovers |
| `verify-configs` | no | Check all stage files |
| `history` | no | `canary/state/history.tsv`: time, user, action, from, to, result, reason |

Options: `--to N`, `--yes`, `--force`, `--dry-run`, `--soak S`, `--no-auto-rollback`,
`--auto-rollback`, `--duration S`, `--window S`, `--reason TEXT`, `--file PATH`.

`./migrate-traffic.sh {0|25|50|75|100}` still works as a deprecated wrapper for
`canary/cutover.sh advance --to N`, and `./migrate-traffic.sh rollback` for `rollback`.

### 7.2 Exit codes

| Code | Meaning | State afterwards |
|-----:|---------|------------------|
| 0 | Success | Target stage live |
| 1 | Usage error or refused (skipped stage, no `--yes`, wrong direction) | Unchanged |
| 2 | Preflight failed | Unchanged |
| 3 | `nginx -t` or reload failed | Previous file restored |
| 4 | Gate breached | Rolled back to the previous stage (unless `--no-auto-rollback`) |
| 5 | New stage not seen live | Rolled back (advance), or installed but not confirmed (rollback/restore), investigate |
| 6 | Another cutover holds the lock | Unchanged |

### 7.3 Files

| Path | Purpose |
|------|---------|
| `nginx-{0,25,50,75,100}percent.conf` | Stage files (repo root, or `CANARY_STAGE_DIR`) |
| `canary/cutover.sh` | The runbook script |
| `canary/canary.env` | Local configuration (git-ignored). Template: `canary/canary.env.example` |
| `canary/state/` | Git-ignored runtime state: `history.tsv`, `cutover.log`, `backups/`, `state` (baseline, previous stage), `lock` |
| `canary/tests/test-stage-configs.sh` | Static checks: markers vs weights, drift between files, `nginx -t` per file, log parser |
| `canary/tests/e2e.sh` | Full cutover against a real nginx and mock backends (about 80 s, no Docker) |

### 7.4 Configuration

Defaults are in `canary/cutover.sh` and `canary/canary.env.example`. Commonly changed settings:

| Variable | Default | |
|----------|---------|-|
| `NGINX_CONF_TARGET` | `/etc/nginx/conf.d/eshop.conf` | Live stage file |
| `NGINX_TEST_CMD` / `NGINX_RELOAD_CMD` | `nginx -t` / `nginx -s reload` | e.g. `docker exec proxy nginx -t` for containerized nginx |
| `CANARY_ACCESS_LOG` | `/var/log/nginx/eshop-canary.log` | Must match the stage files |
| `CANARY_BASE_URL` | `http://localhost` | Proxy entry point for probes |
| `CORE_URL` / `FRAMEWORK_URL` | `http://localhost:5002` / `:5001` | Direct backend URLs for preflight |
| `FRAMEWORK_HEALTH_PATH` | `/` | The Framework app has no dedicated health endpoint |
| `CANARY_PROBE_PATHS` | `/Catalog/ /api/health` | Space-separated |
| `CANARY_SOAK_SECONDS` | `900` | Soak after advance |
| `CANARY_WINDOW_SECONDS` | `300` | Gate window |
| `CANARY_CHECK_INTERVAL` / `CANARY_PROBE_INTERVAL` | `30` / `2` | Seconds |
| `CANARY_REQUIRE_TRAFFIC` | `1` | `0`: not enough traffic at the end of a soak only warns |
| Gate thresholds | see [4.1](#41-automatic-gates) | |

## 8. Troubleshooting

| Symptom | Likely cause / fix |
|---------|--------------------|
| `X-Canary-Stage` missing | The live server block isn't from a stage file (a duplicate server block, or an old include). `nginx -T \| grep canary_stage`. |
| Core share far above the stage % | Few distinct client keys. If nginx sits behind a load balancer, make sure it sets `X-Forwarded-For`, or change the `map` key to a cookie/header that identifies the user. |
| Gates always "not enough data" | Low traffic, or `CANARY_ACCESS_LOG` doesn't match the log nginx writes. Lower `MIN_REQUESTS` for low-traffic environments, or soak longer. |
| Frequent sign-outs after a stage change | Affinity broken: the client key changes per request (e.g. rotating proxy IPs). See the previous row. |
| Exit 6 (lock) | Another `advance`/`rollback`/`restore` is running. `ps aux \| grep cutover.sh`. The lock is released automatically when the process exits. |
| `failover` gate trips with no errors in Application Insights | Core refuses or drops connections (restarts, port exhaustion, deployment during the soak). Check the nginx `error.log`. |

## 9. Changing the stage files

- Make the same change in **all five** `nginx-*percent.conf` files. Only the stage number,
  comments and the `catalog_migration` servers may differ.
- Keep the `eshop_canary` log format fields in the same order. The script parses them by position.
- Run `canary/tests/test-stage-configs.sh` and `canary/tests/e2e.sh` before merging.
- The legacy `nginx.conf` in the repository root is the pre-runbook config (unmarked, 25%). It is
  kept for reference and is **not** managed by the script.

## 10. Assumptions

- nginx runs on the same host as the two apps, so preflight reaches them at `localhost:5001` and
  `localhost:5002`. Change `CORE_URL` / `FRAMEWORK_URL` / `CANARY_BASE_URL` if it doesn't.
- The stage file is included as a single file inside `http {}` and is the only eShop server block.
- The .NET Framework app has no health endpoint, so `/` returning 2xx/3xx counts as healthy.
- The .NET Core app's `/api/health/detailed` returns `"database":{"status":"Healthy",...}`, as
  `HealthController` does today.
- The default thresholds and soak times are conservative starting points taken from the existing
  rollback decision matrix (5xx > 1%, 2× response time). Tune them against real traffic.
- Clients are identified by client IP (first `X-Forwarded-For` hop). Consistent hashing gives
  approximate, not exact, percentages. The share-too-low gate allows `SPLIT_TOLERANCE_PCT` points
  of slack. The key trusts the incoming `X-Forwarded-For`, so a client can choose its backend by
  sending that header. That is acceptable for a canary split, which is not a security control. If
  nginx sits behind a trusted load balancer, use `real_ip` / `set_real_ip_from` and switch the key
  to `$remote_addr`.
- `/api/*` already goes 100% to .NET Core at every stage. This runbook only moves `/Catalog/`.
