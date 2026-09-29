# eShopModernized – local development with Docker Compose

This folder contains the modernized ASP.NET Core (.NET 8) MVC version of the eShop catalog app
(`src/eShopCoreModernized`) and a Docker Compose stack that runs it together with SQL Server for local development.

Unlike the Windows-container compose files elsewhere in this repo, this stack uses **Linux containers** and works
with Docker Desktop (Windows/macOS) or Docker Engine on Linux.

## What gets started

| Service   | Image                                         | Purpose                                                                 | Host port |
|-----------|-----------------------------------------------|-------------------------------------------------------------------------|-----------|
| `sqldata` | `mcr.microsoft.com/mssql/server:2022-latest`  | SQL Server 2022 Developer edition, data persisted in the `sqldata` volume | `5433`    |
| `db-init` | same as above (uses the bundled `sqlcmd`)     | One-shot job: runs `docker/sql/init.sql` to create and seed the catalog DB, then exits | –         |
| `web`     | `eshop/coremodernized:dev` (built locally)    | The ASP.NET Core MVC app                                                 | `5002`    |

Startup order is enforced with health checks: `sqldata` healthy → `db-init` completes successfully → `web` starts.

## Prerequisites

- Docker with the Compose v2 plugin (`docker compose version`), Linux containers enabled.
- ~2 GB free RAM for SQL Server. On Apple Silicon, enable Rosetta/amd64 emulation in Docker Desktop
  (the SQL Server image is amd64-only).

## Quick start

From this folder (`eShopModernized/`):

```bash
cp .env.example .env          # optional – defaults work without it
docker compose up --build -d --wait
```

Then open:

- App: http://localhost:5002
- Health: http://localhost:5002/api/health and http://localhost:5002/api/health/detailed (includes DB connectivity)

The catalog list is public. Create/Edit/Delete require sign-in: the app uses a local cookie-auth stub, so **any
non-empty username and password** is accepted at `/Account/Login`.

## Everyday workflow

| Task                                   | Command                                                        |
|----------------------------------------|----------------------------------------------------------------|
| Rebuild and restart the app after code changes | `docker compose up --build -d --wait web`              |
| Tail app logs                          | `docker compose logs -f web`                                   |
| Check status / health                  | `docker compose ps`                                            |
| Stop everything (keep data)            | `docker compose down`                                          |
| Stop and wipe the database             | `docker compose down -v`                                       |
| Re-run the seed script                 | `docker compose run --rm db-init`                              |
| Open a SQL shell in the container      | `docker compose exec sqldata bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d Microsoft.eShopOnContainers.Services.CatalogDb'` |

You can also connect from the host with SSMS / Azure Data Studio / VS Code SQL tools:
server `localhost,5433`, user `sa`, password from `MSSQL_SA_PASSWORD` (default `Pass@word1`), trust server certificate.

### Running the app from your IDE against the containerized database

Start only the database and seed it: `docker compose run --rm db-init` (this starts `sqldata`, waits for it to be
healthy, runs the seed script and exits). Then run the app (requires the .NET 8 SDK) from
`src/eShopCoreModernized` with the connection string pointed at the published port, e.g.:

```bash
cd src/eShopCoreModernized
ConnectionStrings__CatalogDBContext="Server=localhost,5433;Database=Microsoft.eShopOnContainers.Services.CatalogDb;User Id=sa;Password=Pass@word1;TrustServerCertificate=True" \
AppSettings__UseMockData=false \
dotnet run
```

## Configuration

Settings are read from `.env` in this folder (see `.env.example`):

| Variable              | Default                                           | Notes |
|-----------------------|---------------------------------------------------|-------|
| `MSSQL_SA_PASSWORD`   | `Pass@word1`                                      | Must meet SQL Server complexity rules. Changing it after the volume exists requires `docker compose down -v`. |
| `ESHOP_DB_NAME`       | `Microsoft.eShopOnContainers.Services.CatalogDb`  | Same name as the legacy/modernized .NET Framework apps. |
| `ESHOP_WEB_PORT`      | `5002`                                            | Host port for the app. |
| `ESHOP_SQL_PORT`      | `5433`                                            | Host port for SQL Server. |
| `ESHOP_USE_MOCK_DATA` | `false`                                           | `true` runs the app with the in-memory mock catalog (DB still starts but is unused). |

The app is configured through standard ASP.NET Core environment variables in `docker-compose.yml`
(`ConnectionStrings__CatalogDBContext`, `AppSettings__*`). All Azure integrations (Blob Storage, Managed Identity,
Azure AD) are disabled for local development, and `ASPNETCORE_ENVIRONMENT=Development` skips Key Vault.

## Database schema and seed data

`docker/sql/init.sql` is idempotent and:

- creates the database if it does not exist;
- creates the `CatalogBrand`, `CatalogType` and `Catalog` tables matching the EF Core model in `Models/CatalogDBContext.cs`;
- creates the `catalog_hilo` sequence used by `CatalogItemHiLoGenerator` to assign new item IDs (starts at 101 so it
  never collides with the seeded IDs 1–12);
- seeds the same 5 brands, 4 types and 12 catalog items as the original eShop sample, only if the tables are empty.

The app still calls `EnsureCreated()` at startup; because the schema already exists this is a no-op. If you change the
EF model, update `init.sql` accordingly and recreate the database with `docker compose down -v`.

## Troubleshooting

- **`web` never becomes healthy** – check `docker compose logs web`; most often the SA password in `.env` differs from
  the one the `sqldata` volume was initialized with. Run `docker compose down -v` and start again.
- **`db-init` exits non-zero** – `docker compose logs db-init` shows the failing SQL statement.
- **Port already in use** – change `ESHOP_WEB_PORT` / `ESHOP_SQL_PORT` in `.env`.

## Known limitations

- Product images do not render: `ImageMockStorage` builds `/pics/{id}/{file}` URLs that don't map to any route or
  static file, and `wwwroot/Pics` only ships `dummy.png`. This is an existing app issue, not a compose issue.
- Data-protection keys live inside the container, so sign-in cookies are invalidated whenever `web` is recreated.
