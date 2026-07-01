# eShopModernized.Tests

Unit tests for `eShopModernized/src/eShopCoreModernized` (the `eShopCoreModernized`
ASP.NET Core 8.0 web app). Built with **xUnit**, **Moq**, and the **EF Core
in-memory** provider, plus an optional throw-away **SQL Server** database for the
code paths that depend on a real server.

## Running the tests

```bash
cd eShopModernized/tests/eShopModernized.Tests
dotnet test
```

All tests pass without any external infrastructure. The tests that need SQL
Server (see below) skip automatically when no server is reachable.

### SQL Server–dependent tests

`CatalogItemHiLoGenerator` and `CatalogService.CreateCatalogItem[Async]` issue
`SELECT NEXT VALUE FOR catalog_hilo`, which only runs on SQL Server (not on the
in-memory / SQLite providers). `SqlServerFixture` provisions a temporary database
for these tests and tears it down afterwards.

By default it connects to `Server=127.0.0.1,1433` with the `sa` account. Point it
elsewhere with the `TEST_SQL_CONNECTION` environment variable:

```bash
export TEST_SQL_CONNECTION="Server=127.0.0.1,1433;User Id=sa;Password=Str0ng!Passw0rd;TrustServerCertificate=True;Encrypt=False"
dotnet test
```

A local server can be started with Docker:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Str0ng!Passw0rd" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

When no server is available these tests report as **skipped** (via
`Xunit.SkippableFact`) and `dotnet test` still succeeds.

## Coverage report

```bash
# collect coverage
dotnet test --collect:"XPlat Code Coverage"

# generate a per-class report (install the tool once)
dotnet tool install -g dotnet-reportgenerator-globaltool
export DOTNET_ROOT="$(dirname "$(which dotnet)")"
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/report" \
  -reporttypes:"TextSummary;Html"

cat TestResults/report/Summary.txt
```

## Coverage status

Line coverage is **≥ 80% for every testable class**. Latest run:

| Class | Line coverage |
| --- | --- |
| `CatalogConfiguration` | 100% |
| `AccountController` | 100% |
| `BrandsController` | 100% |
| `CatalogController` | 100% |
| `FilesController` | 100% |
| `HealthController` | 100% |
| `ImageUploadController` | 100% |
| `PicController` | 82% |
| `AppSettingsSqlConnectionFactory` | 100% |
| `ManagedIdentitySqlConnectionFactory` | 77% (see note) |
| `MigrationTelemetryInitializer` | 100% |
| `CatalogBrand` / `CatalogItem` / `CatalogType` | 100% |
| `CatalogDBContext` | 100% |
| `CatalogItemHiLoGenerator` | 100% |
| `CatalogService` | 100% |
| `CatalogServiceMock` | 100% |
| `ImageAzureStorage` | 96% |
| `ImageMockStorage` | 100% |
| `PaginatedItemsViewModel<T>` | 100% |

### Documented gaps requiring live infrastructure

- **`ManagedIdentitySqlConnectionFactory` (77%)** — the final lines assign the
  acquired AAD access token to the connection and return it. Reaching them
  requires a *successful* `DefaultAzureCredential.GetToken` call, which only
  works inside a live Azure / managed-identity environment. The test exercises
  everything up to (and including) the token request; the credential-unavailable
  path is asserted instead. The successful path cannot be unit-tested here.

Classes reported at 0% that are **not** unit-testable and are intentionally
excluded: `Program` (top-level web-host bootstrap) and the Razor
`AspNetCoreGeneratedDocument.Views_*` view classes.

## Layout

```
Common/           Shared test helpers
  TestData.cs         Factory methods for brands / types / items
  InMemoryContext.cs  Builds EF Core in-memory CatalogDBContext instances
  SqlServerFixture.cs xUnit fixture for the SQL-Server-only tests (skippable)
  MvcTestContext.cs   Wires up ControllerContext / RequestServices for controllers
Controllers/      Controller tests (Groups A, B, C)
Services/         Service tests (Group D)
Configuration/    CatalogConfiguration tests (Group E)
Models/           POCO model + HiLo generator tests (Group E)
ViewModel/        PaginatedItemsViewModel tests (Group E)
Infrastructure/   Telemetry + SqlConnectionFactory tests (Group E)
```
