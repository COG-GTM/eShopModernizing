# Linux containers for the .NET 8 storefront (T7)

Multi-stage images (`mcr.microsoft.com/dotnet/sdk:8.0` → `mcr.microsoft.com/dotnet/aspnet:8.0`) for both apps, running as the
image's non-root `app` user (`$APP_UID`, 1654) on container port 8080. Build context is `net8/`; `Dockerfile.<app>.dockerignore`
(BuildKit per-Dockerfile ignore file) keeps host `bin/`/`obj/` and `tests/` out of the context.

| File | Purpose |
|---|---|
| `Dockerfile.mvc`, `Dockerfile.webforms` | restore (csproj layer) → `dotnet publish -c Release` → aspnet runtime; healthcheck over bash `/dev/tcp` (the image has no curl/wget) |
| `compose.mock.yml` | mock mode (`UseMockData=true`): MVC `127.0.0.1:5200`, WebForms `127.0.0.1:5201` |
| `compose.sql.yml` | SQL mode (`UseMockData=false`): SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`, `127.0.0.1:14331`) with a `sqlcmd` healthcheck; MVC `5210`, WebForms `5211`; one database per app (`eShopStorefrontCatalog_mvc` / `_webforms`), `Encrypt=False`; apps start only after SQL is healthy |
| `stack.sh` | `up` / `down` / `reset <app>` / `seed` / `url` for either mode |
| `replay.sh` | replays the legacy transcripts against the containers and checks the acceptance |
| `sql_parity.py` | compares the SQL-mode replay with legacy SQL mode (`modernization/baseline/sql-mode`) and checks the seed counts |

## Legacy → .NET 8 container mapping

| Legacy (.NET Framework 4.7.2) | .NET 8 |
|---|---|
| Legacy apps hosted on Windows/IIS (.NET Framework 4.7.2; the repo's root `docker-compose.yml` only builds Windows images of the earlier modernized variants) | Linux `aspnet:8.0`, Kestrel on 8080, non-root |
| `Web.config` appSettings `UseMockData` / `UseCustomizationData` | env vars `UseMockData` / `UseCustomizationData` (same keys) |
| `Web.config` connection string `CatalogDBContext` | env var `ConnectionStrings__CatalogDBContext` |
| Legacy harness on Mono: MVC `:5100`, WebForms `:5101` (mock); `:5110`/`:5111` + SQL `:14330` (SQL mode) | mock `:5200`/`:5201`; SQL `:5210`/`:5211` + SQL `:14331` (both stacks and the legacy harness can run side by side) |
| EF6 `CreateDatabaseIfNotExists` per app DB | EF Core initializer (T1) per app DB; `stack.sh sql reset` drops the DB so the app re-creates and re-seeds it |

## Usage

```bash
pip install -r modernization/tools/requirements.txt

# mock mode
docker compose -f net8/docker/compose.mock.yml up -d --build --wait     # or: net8/docker/stack.sh mock up
net8/docker/replay.sh mock [--no-build] [--tests]                       # expects mvc 86/86 + webforms 52/52

# SQL mode (stack.sh generates a throwaway SA password into net8/docker/.sql.env, git-ignored; or export MSSQL_SA_PASSWORD,
# which is embedded unquoted in the connection string: avoid `;`, `=`, quotes)
net8/docker/stack.sh sql up
net8/docker/stack.sh sql seed                                            # 12 items / 5 brands / 4 types / 3 HiLo sequences per DB
net8/docker/replay.sh sql [--no-build]                                   # replay + sql_parity.py vs legacy SQL mode

net8/docker/stack.sh mock down; net8/docker/stack.sh sql down
```

Raw compose commands for SQL mode need the env file: `docker compose -f net8/docker/compose.sql.yml --env-file net8/docker/.sql.env ...`.

Replays always start from fresh state: `transcript.py replay --reset-cmd` calls `stack.sh <mode> reset <app>`, which recreates the
app container (new in-memory catalog) and, in SQL mode, first drops the app's database. Reports go to `net8/docker/results/`
(git-ignored): `<mode>-<app>.json`, `sql-seed.csv`, `sql-parity.json`, `mock-characterization.xml`.

## Acceptance checks

- **Mock:** `replay.sh mock` exits 0 only if all 138 transcripts (mvc 86 + webforms 52) match the mock-mode baseline.
- **SQL:** `replay.sh sql` exits 0 only if both databases seed 12/5/4 (+3 sequences) and `sql_parity.py` finds, per case, the same
  outcome and identical diffs as legacy SQL mode. Legacy SQL mode was MVC 85/86 (only `071-api-files`: 721 vs 719 bytes) and
  WebForms 29/52 (23 price-formatting diffs, `19.50` vs `19.5`, CONTRACT.md §6). `071-api-files` is held to the mock bytes, so the
  expected .NET 8 SQL result is MVC 86/86 and WebForms 29/52 with exactly the legacy price-formatting diffs.
