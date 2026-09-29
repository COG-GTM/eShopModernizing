# eShop Strangler Fig Migration - Status Documentation

> **Status: in progress, not complete.** The checked-in `nginx.conf` is the **25% phase**
> configuration. The .NET Core app (`eShopModernized/src/eShopCoreModernized`) serves
> `/api/*` and 25% of `/Catalog/*` requests; everything else is still served by the
> modernized .NET Framework app. The file name is kept for link stability.
>
> Last reconciled against the repository contents on 2026-09-29. Statements below are
> either traceable to a file in this repo or explicitly marked as *not verified*.

## Current Architecture

### Traffic Routing (`nginx.conf`, 25% phase)

| nginx `location` | Upstream | Backend(s) |
|------------------|----------|------------|
| `/api/health`, `/api/health/detailed` | `core_backend` | .NET Core (`localhost:5002`) |
| `/api/` | `core_backend` | .NET Core (`localhost:5002`) |
| `/Catalog/` | `catalog_migration` | `localhost:5001` weight 3, `localhost:5002` weight 1 (~75% / ~25% round-robin) |
| `/` (everything else) | `modernized_backend` | Modernized .NET Framework (`localhost:5001`) |

Notes:
- `location /Catalog/` only matches paths with the trailing slash (`/Catalog/Edit/1`, etc.).
  `/`, which renders the catalog index in both apps, and `/Catalog` without a trailing
  slash are served by `localhost:5001`.
- Image paths used by the UI (`/pics/...`, `/items/{id}/pic`, `/uploadimage`) fall through to
  `location /` and are therefore served by the .NET Framework app. The .NET Core UI's
  `wwwroot/js/site.js` posts image uploads to `/uploadimage`, a route that only exists in the
  .NET Framework app (`PicController`); the .NET Core equivalent is `POST /api/ImageUpload/upload`.
- The upstream weighting has no session affinity. Catalog Create/Edit/Delete are
  `[Authorize]` + `[ValidateAntiForgeryToken]` in the .NET Core app, and the two apps do not
  share authentication cookies or anti-forgery keys, so a form rendered by one backend and
  posted to the other is expected to fail (*not verified end-to-end*).

### Service Ports

| Port | Referenced by | What is actually there |
|------|---------------|------------------------|
| `5000` | `upstream legacy_backend` | Defined in every nginx config but **not used by any `location`**. Nothing in the repo binds the legacy app to 5000. Note: Kestrel's default port is also 5000, so `dotnet run` of the .NET Core app (which has no `launchSettings.json`) listens on 5000, not 5002. |
| `5001` | `upstream modernized_backend`, 75% of `catalog_migration` | Assumed to be the modernized .NET Framework MVC app (`eShopModernizedMVCSolution`). Nothing in the repo binds it to 5001; in `docker-compose.override.yml` that app is published on `5115`. |
| `5002` | `upstream core_backend`, 25% of `catalog_migration` | .NET Core app. Only `docker-compose.override.yml` maps it (`5002:80`); locally, set `ASPNETCORE_URLS=http://localhost:5002`. |

The legacy .NET Framework app (`eShopLegacyMVCSolution`) has **not** been decommissioned; its
code is still in the repo.

### .NET Core application facts

- Target framework: **.NET 8** (`<TargetFramework>net8.0</TargetFramework>`, EF Core 8.0.8).
  The `version` field of `/api/health/detailed` and the `ApplicationVersion` telemetry property
  are hard-coded to `".NET Core 6.0"` and are wrong.
- Assembly name: `eShopCoreModernized` (project file is `eShopModernized.csproj`).
- `dotnet build` succeeds (with nullable-reference warnings).
- There are **no automated tests** and **no CI pipeline** in this repository.

## Feature Flags

Read by `Configuration/CatalogConfiguration.cs` from the `AppSettings` section. Defaults in
`appsettings.json`:

| Flag (config key) | Default | Effect in `Program.cs` |
|-------------------|---------|------------------------|
| `AppSettings:UseMockData` | `true` | In-memory `CatalogServiceMock` (5 items). When `false`, `CatalogService` + `Database.EnsureCreated()` at startup. |
| `AppSettings:UseAzureStorage` | `false` | `ImageAzureStorage` + `BlobServiceClient` from `Azure:StorageConnectionString`; otherwise `ImageMockStorage`. |
| `AppSettings:UseAzureManagedIdentity` | `false` | Registers `ManagedIdentitySqlConnectionFactory` instead of `AppSettingsSqlConnectionFactory`. |
| `AppSettings:UseAzureActiveDirectory` | `false` | OpenID Connect via `Microsoft.Identity.Web` (`Azure:ActiveDirectory` section); otherwise local cookie auth. |
| `AppSettings:UseCustomizationData` | `false` | Read into configuration but not used anywhere. |

Flags are evaluated once at startup; changing them requires restarting the app.

**Environment variables must use the hierarchical form** (`AppSettings__UseMockData`,
`ConnectionStrings__CatalogDBContext`, `Azure__StorageConnectionString`, ...). The flat names
used for `eshop.core.modernized` in `docker-compose.override.yml` (`UseMockData`,
`CatalogDBContext`, `StorageConnectionString`, `AppInsightsConnectionString`,
`AzureActiveDirectoryClientId`, ...) are **ignored** by the .NET Core app. Verified: starting the
app with `UseMockData=False UseAzureStorage=True` still reports `useMockData: true,
useAzureStorage: false` from `/api/health/detailed`.

## Azure Integrations

Code exists for each integration; **none has been verified against real Azure resources** (the
repo has no Azure resource definitions or test evidence for this app).

| Service | What the code does | Caveats |
|---------|--------------------|---------|
| Azure Key Vault | Added as a configuration source when not `Development` **and** `Azure:KeyVaultName` is set (empty by default). Uses `DefaultAzureCredential`. | Off by default. |
| Azure Blob Storage | `ImageAzureStorage` reads/writes the `pics` container. | With `UseAzureStorage=true` and no connection string, every request (including `/api/health`) returns 500 (`ArgumentNullException: connectionString`) - verified locally. |
| Application Insights | `AddApplicationInsightsTelemetry` using `Azure:ApplicationInsights:ConnectionString` (empty by default); adaptive sampling off; Live Metrics (QuickPulse) on. | No telemetry is sent until a connection string is configured. |
| Azure SQL + Managed Identity | `ManagedIdentitySqlConnectionFactory` fetches an AAD token for SQL. | The factory is registered but **never used**: `CatalogDBContext` is configured with `UseSqlServer(connectionString)` directly, so `UseAzureManagedIdentity` has no effect on data access. |
| Azure Active Directory | `AddMicrosoftIdentityWebApp(Azure:ActiveDirectory)`. | With AAD off, `AccountController.Login` signs in **any** non-empty username/password. |

## Monitoring and Health Checks

### Health Endpoints (`Controllers/HealthController.cs`)

The endpoints are under `/api`, not `/health`:

- `GET /api/health` - always returns `{"status":"Healthy", ...}` if the process is up.
- `GET /api/health/detailed` - returns configuration flags plus database and (if enabled) blob
  storage status.

Known limitations (verified locally):
- The top-level `status` is hard-coded to `"Healthy"`.
- The database check ignores the result of `CanConnectAsync()` (which returns `false` rather than
  throwing), so the database is reported `"Healthy"` even when unreachable - observed with the
  default LocalDB connection string on Linux, where LocalDB is not supported.
- `/health` and `/health/detailed` return 404 on the .NET Core app, and through nginx they are
  routed to `localhost:5001`, which has no health endpoint.

### Application Insights

The only custom telemetry is `Infrastructure/MigrationTelemetryInitializer.cs`, which stamps
static properties on every item: `ApplicationVersion=".NET Core 6.0"` (wrong, app is .NET 8),
`MigrationPhase="StranglerFig-Complete"` (wrong, rollout is at 25%), and
`ServiceType="Catalog-Core"`. There are no dashboards, alert rules, or legacy-vs-core
performance comparisons defined in this repository.

## Traffic Migration Strategy

nginx upstream weighting on `/Catalog/` only:

| Phase | File | `catalog_migration` upstream |
|-------|------|------------------------------|
| 25% | `nginx-25percent.conf` (identical to current `nginx.conf`) | 5001 weight 3, 5002 weight 1 |
| 50% | `nginx-50percent.conf` | 5001 weight 1, 5002 weight 1 |
| 75% | `nginx-75percent.conf` | 5001 weight 1, 5002 weight 3 |
| 100% | `nginx-100percent.conf` | 5002 only |

There is no 0% file; see [ROLLBACK-PROCEDURES.md](./ROLLBACK-PROCEDURES.md).

All configs contain only `upstream`/`server` blocks, so they must be `include`d inside an
`http {}` block (e.g. from `/etc/nginx/conf.d/`). `nginx -t -c nginx.conf` on the raw file fails
(`"upstream" directive is not allowed here`); wrapped in `http {}`, all seven configs
(four phases, `nginx.conf`, two backups) pass `nginx -t`.

### Migration Script (`migrate-traffic.sh {25|50|75|100}`)

What it actually does:
1. Copies `./nginx.conf` to `./nginx-backups/nginx-<epoch>.conf`.
2. Copies the phase file over `./nginx.conf` **in the repository working directory**.
3. Runs `curl -s http://localhost/api/health` once per second for 300 seconds, printing `.` or `X`.

What it does **not** do:
- It does not install the config into nginx or reload nginx (`NGINX_CONFIG_DIR=/etc/nginx` is
  declared but unused). Unless the live nginx includes this file directly, you must copy it into
  place and run `nginx -t && nginx -s reload` yourself.
- It does not validate the config (`nginx -t`) before applying it.
- The health loop only detects connection failures: `curl -s` without `-f` exits 0 on HTTP
  4xx/5xx. It does not stop, alert, or roll back on failure.
- It has no 0% option and cannot restore a backup.

## Legacy System Decommission

**Not started.** Preconditions that are not yet met:
1. `/Catalog/` is at 25%, not 100%, and `/` still goes to `localhost:5001`.
2. The legacy (`eShopLegacyMVCSolution`) and modernized .NET Framework
   (`eShopModernizedMVCSolution`) apps are still required and still in the repo.
3. The .NET Core UI still depends on the .NET Framework app for `/uploadimage` and images.

When these are met: confirm no traffic reaches `localhost:5000`/`5001` via nginx access logs,
remove the unused `legacy_backend` upstream, then remove the legacy containers and code.

## Feature Validation

No feature-flag combination has been validated. `validate-features.ps1` exists but does not
work as written:
- It probes `http://localhost:5002/health/detailed`: wrong path (`/api/health/detailed`) and
  wrong port (`dotnet run` binds 5000; nothing sets 5002). Running it locally with PowerShell 7
  produced `Health check failed: Connection refused (localhost:5002)`.
- Failures are not detected: `Test-Configuration` also emits `dotnet build` output, so
  `$result` is an array and `-not $result` is never true (reproduced with a minimal PowerShell
  function).
- It overwrites the tracked `appsettings.json` and does not restore it.
- It stops processes named `dotnet`, but the started app runs as `eShopCoreModernized`, so the app
  keeps running on port 5000 after the script.
- Configurations 2 and 3 set `UseMockData=false` and `UseAzureStorage=true`, which need a real SQL
  Server and storage connection string; with the defaults the app aborts at startup
  (`PlatformNotSupportedException: LocalDB is not supported on this platform` on Linux).

Configurations the script intends to cover:
1. Development: mock data, no Azure services
2. Staging: Azure Storage + Managed Identity, no AAD
3. Production: all Azure services enabled

## Success Criteria

| Criterion | Status |
|-----------|--------|
| 100% of catalog traffic routed to .NET Core | Not met - 25% of `/Catalog/`; `/` and images still on 5001 |
| All Azure integrations working | Not verified; Managed Identity for SQL is not wired to `CatalogDBContext` |
| Performance meets or exceeds legacy | Not measured - no benchmarks or baseline in the repo |
| Error rate below 1% | Not measured - no telemetry configured |
| All feature flags tested | Not met - see Feature Validation |
| Monitoring and alerting in place | Not met - health checks always report Healthy; no alert rules |
| Rollback procedures documented and tested | Documented; not tested |

## Operational Procedures

### Deployment (as the repo stands today)

1. `dotnet build eShopModernized/src/eShopCoreModernized/eShopModernized.csproj` (requires the .NET 8 SDK).
2. Configure via `appsettings.json` or hierarchical environment variables (see Feature Flags).
   Key Vault is only used if `Azure:KeyVaultName` is set and the environment is not `Development`.
3. Run with `ASPNETCORE_URLS=http://localhost:5002` so it matches `core_backend` in nginx.
4. Check `curl -f http://localhost:5002/api/health/detailed` (see its limitations above).

The container path is currently broken:
- `eShopModernized/src/eShopCoreModernized/Dockerfile` uses `dotnet/sdk:6.0`/`aspnet:6.0` images
  for a `net8.0` project; `docker build` fails with `NETSDK1045: The current .NET SDK does not
  support targeting .NET 8.0` (verified). Its `ENTRYPOINT` also references `eShopModernized.dll`,
  but the assembly is `eShopCoreModernized.dll`.
- `docker-compose.yml` builds `eshopwcfservice`, `eshop.modernized.webforms` and
  `eshop.modernized.mvc` from `./deploy/{wcf,webforms,mvc}`, which do not exist in the repo.
- `sql.data` is a Windows container image on the external `nat` network, while the .NET Core
  image is Linux-based.

### Rollback

See [ROLLBACK-PROCEDURES.md](./ROLLBACK-PROCEDURES.md).

## Configuration Files

### Nginx Configurations
- `nginx.conf`: current configuration - **25% phase** (identical to `nginx-25percent.conf`).
- `nginx-25percent.conf`, `nginx-50percent.conf`, `nginx-75percent.conf`, `nginx-100percent.conf`: phase configs.
- `nginx-backups/nginx-1757900042.conf`: 100% config with the **old, wrong** `/health` and
  `/health/detailed` locations (pre-fix). Do not restore it.
- `nginx-backups/nginx-1757900222.conf`: identical to `nginx-100percent.conf`.

### Docker Compose
- `docker-compose.yml` / `docker-compose.override.yml`: define `eshop.core.modernized` on port
  5002; see the broken-container notes above.

### Scripts
- `migrate-traffic.sh`: copies phase configs and polls `/api/health`; see its limitations above.
- `validate-features.ps1`: intended flag validation; currently non-functional (see above).

## Troubleshooting

1. **Health check says Healthy but the app misbehaves**: `/api/health*` cannot report unhealthy
   for the database; check application logs instead.
2. **Every request returns 500 after enabling Azure Storage**: `Azure:StorageConnectionString`
   (env `Azure__StorageConnectionString`) is missing.
3. **Flags set via docker-compose have no effect**: use hierarchical names (`AppSettings__UseMockData`).
4. **App crashes on startup with `UseMockData=false`**: `ConnectionStrings:CatalogDBContext`
   defaults to LocalDB, which is Windows-only; provide a reachable SQL Server.
5. **Catalog form posts fail intermittently**: likely the lack of session affinity across 5001/5002 (see Traffic Routing).
6. **Traffic distribution**: check nginx access logs (`$upstream_addr`); no other tooling exists.

## Next Steps

1. Fix `HealthController` (real DB result, aggregate status, correct version string) so health
   checks can gate rollouts.
2. Fix the Dockerfile (SDK/runtime 8.0, `eShopCoreModernized.dll`) and the compose env var names.
3. Wire `ISqlConnectionFactory` (or an EF Core interceptor) into `CatalogDBContext` if Managed
   Identity is required.
4. Add session affinity (e.g. `hash $cookie_...`/`ip_hash`) or share data-protection keys before
   raising the `/Catalog/` weight.
5. Make `migrate-traffic.sh` validate and reload nginx, use `curl -f`, and add a 0% phase.
6. Fix `validate-features.ps1` (path, port, return values, restore `appsettings.json`).
7. Add automated tests and CI; configure Application Insights and alert rules.
