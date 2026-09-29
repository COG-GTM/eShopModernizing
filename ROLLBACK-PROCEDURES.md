# Strangler Fig Migration Rollback Procedures

> Reconciled against the repository on 2026-09-29. **None of these procedures has been
> exercised end-to-end**; treat them as a runbook to rehearse, not a tested procedure.

## Before You Start

- **Current state**: `nginx.conf` is the **25% phase** (identical to `nginx-25percent.conf`):
  `/api/*` → .NET Core (`localhost:5002`); `/Catalog/*` → ~75% `localhost:5001` / ~25%
  `localhost:5002`; everything else → `localhost:5001`. See [MIGRATION-COMPLETE.md](./MIGRATION-COMPLETE.md).
- **The repo's `nginx.conf` is not the live nginx config.** The files in the repo contain only
  `upstream`/`server` blocks and must be included inside `http {}` (e.g. copied to
  `/etc/nginx/conf.d/eshop.conf`). Every step below that changes a config must be followed by
  installing it and reloading:
  ```bash
  sudo cp nginx.conf /etc/nginx/conf.d/eshop.conf   # adjust to wherever your nginx includes it
  sudo nginx -t && sudo nginx -s reload
  ```
  `migrate-traffic.sh` does **not** do this for you; it only rewrites `./nginx.conf` in the repo.
- **Health endpoints are `/api/health` and `/api/health/detailed`** and only exist on the .NET Core
  app. `/health` returns 404 on .NET Core and is routed by nginx to `localhost:5001`, which has no
  health endpoint. Neither `/api/health` nor `/api/health/detailed` can report the database as
  unhealthy (the `CanConnectAsync()` result is ignored), so use application logs and real page
  requests to judge health.

## Quick Rollback (Emergency)

Goal: stop sending `/Catalog/` traffic to .NET Core.

1. **Route all `/Catalog/` traffic back to the .NET Framework app (0% .NET Core).**
   There is no `nginx-0percent.conf` and `migrate-traffic.sh` has no `0` option. Either remove
   the 5002 server from the `catalog_migration` upstream:
   ```bash
   cp nginx.conf "nginx-backups/nginx-$(date +%s).conf"
   sed -i '/upstream catalog_migration/,/}/{/localhost:5002/d}' nginx.conf
   ```
   or restore a known backup:
   ```bash
   cp nginx-backups/<file>.conf nginx.conf
   ```
   Backup caveats:
   - `nginx-backups/nginx-1757900042.conf` is a 100% config with the old, wrong `/health` and
     `/health/detailed` locations. Do not restore it.
   - `nginx-backups/nginx-1757900222.conf` is identical to `nginx-100percent.conf` (100% .NET
     Core). Restoring it is a roll-*forward*, not a rollback.
   - Backups written by `migrate-traffic.sh` are named `nginx-<unix-epoch>.conf`; the one with the
     highest epoch is the config that was live before the last script run.

   Then install and reload nginx (see *Before You Start*).

2. **Optional: also roll back `/api/*`.** The phase configs always send `/api/` to .NET Core, and
   the modernized .NET Framework app on 5001 has **no** `/api` endpoints. The only fallback is the
   original legacy app (`eShopLegacyMVCSolution`, Web API routes `api/{controller}/{id}` for
   `brands`/`files`), which would have to be running on `localhost:5000` (`legacy_backend`
   upstream, currently unused). If it is, change `proxy_pass http://core_backend;` to
   `proxy_pass http://legacy_backend;` in the `location /api/` block. Leave `/api/health*` on
   `core_backend`. The original pre-migration config (all traffic to 5000) is available with
   `git show 2b50e97:nginx.conf`.

3. **Verify.**
   ```bash
   # .NET Framework app answers catalog pages through nginx (expect 200 every time)
   for i in $(seq 1 8); do curl -s -o /dev/null -w '%{http_code}\n' http://localhost/Catalog/; done
   # .NET Core process is still up (the endpoint only exists on 5002)
   curl -f http://localhost:5002/api/health
   ```
   Confirm in the nginx access log (`$upstream_addr`, if logged) that `/Catalog/` requests go
   only to `localhost:5001`.

## Gradual Rollback

1. **Step down the `/Catalog/` weight** (from the current 25%, only the last step applies):
   - 100% → 75%: `./migrate-traffic.sh 75`
   - 75% → 50%: `./migrate-traffic.sh 50`
   - 50% → 25%: `./migrate-traffic.sh 25`
   - 25% → 0%: see Quick Rollback step 1 (no script support)

   After each script run, install and reload nginx yourself. The script then blocks for 300 s
   polling `http://localhost/api/health` and prints `.`/`X`; `X` only means the connection failed
   (`curl -s` without `-f` treats HTTP 5xx as success) and the script takes no action on it.
   Because `/api/health` is always served by .NET Core, this check says nothing about the
   `/Catalog/` backends you are shifting traffic between.

2. **Monitor during rollback**
   - nginx access/error logs for 5xx and upstream errors (the only traffic telemetry that exists
     today).
   - Application Insights, *if* `Azure:ApplicationInsights:ConnectionString` has been configured
     (empty by default, so no telemetry is sent). No dashboards or alert rules are defined in this
     repo.
   - Both apps share the `Microsoft.eShopOnContainers.Services.CatalogDb` database and the
     `catalog_hilo` sequence, so traffic rollback needs no data migration. Spot-check that items
     created via .NET Core are visible in the .NET Framework app.

## Common Rollback Scenarios

Thresholds are listed once in the decision matrix below. Nothing in the repo measures or alerts on
them; they must be checked manually from logs (or App Insights, once configured).

### High error rate / database issues
Immediate rollback of `/Catalog/` (Quick Rollback step 1). Also consider `/api/*` (step 2).

### Performance degradation
Gradual rollback, one step at a time, observing logs between steps.

### Intermittent form failures (Create/Edit/Delete)
Likely caused by round-robin between 5001 and 5002 without session affinity: the .NET Core
actions are `[Authorize]` + `[ValidateAntiForgeryToken]` and the two apps do not share cookies or
anti-forgery keys. Roll back to 0% (or add `ip_hash`/cookie affinity to `catalog_migration`).

### Azure service issues (.NET Core app)
- Feature flags are read once at startup; changing them requires restarting the .NET Core app.
- Set flags with hierarchical names, e.g. `AppSettings__UseAzureStorage=false`. The flat names in
  `docker-compose.override.yml` (`UseAzureStorage=...`) are ignored.
- `AppSettings:UseAzureStorage=false` switches images to `ImageMockStorage` (URLs under `/pics/`,
  served via nginx by the .NET Framework app).
- `AppSettings:UseAzureManagedIdentity` has no effect on data access (the SQL connection factory
  it selects is never used), so toggling it will not fix database auth problems; fix
  `ConnectionStrings:CatalogDBContext` instead.
- `UseAzureStorage=true` with an empty `Azure:StorageConnectionString` makes **every** request,
  including `/api/health`, return 500.

### Authentication issues
Setting `AppSettings:UseAzureActiveDirectory=false` on .NET Core falls back to local cookie auth
whose login page accepts **any** non-empty username/password. That is not a safe production
rollback; prefer routing `/Catalog/` back to 5001 instead.

## Post-Rollback Actions

1. Save the nginx access/error logs and .NET Core application logs covering the incident.
2. Record which config was live (`nginx-backups/` and `git diff nginx.conf`) and commit or revert
   the repo's `nginx.conf` so it matches the live config.
3. Identify root cause and plan remediation.
4. Schedule the next migration attempt.

## Rollback Validation

After any rollback:
1. Catalog list, details, create, edit and delete work through `http://localhost/` (sign-in
   required for create/edit/delete).
2. Images render and image upload works (served by the .NET Framework app).
3. Database connectivity: load a page backed by real data; do not rely on
   `/api/health/detailed`, which always reports the database as Healthy.
4. Watch nginx 5xx rates for 30 minutes.
5. Confirm sign-in works on the backend now serving `/Catalog/`.

## Emergency Contacts

Not defined in this repository. Fill in before relying on this runbook:
- DevOps Team: TBD
- Database Administrator: TBD
- Azure Support: TBD

## Rollback Decision Matrix

Proposed thresholds (not enforced or monitored by any tooling in this repo):

| Issue Type | Threshold | Action |
|------------|-----------|--------|
| HTTP 5xx on `/Catalog/` or `/api/` | >1% of requests sustained for 5 minutes | Immediate rollback to 0% |
| Response time | >2x the pre-change baseline, or >5 s average | Gradual rollback |
| Azure service outage | Any dependency down | Disable the affected flag and restart, or roll back to 0% |
| Database errors | Any sustained connection/query failures | Immediate rollback to 0% |
| Authentication failures | Users cannot sign in | Roll back `/Catalog/` to 0%; do not disable AAD in production |
