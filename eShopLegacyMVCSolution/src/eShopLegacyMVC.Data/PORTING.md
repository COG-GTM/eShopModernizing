# eShopLegacyMVC catalog data layer: EF6 / .NET Framework -> EF Core 8 / .NET 8

`eShopLegacyMVC.Data` is a `net8.0` class library containing the port of the catalog data-access layer from
`src/eShopLegacyMVC` (Models, Models/Infrastructure, Services, ViewModel). The legacy project is unchanged. So is the
older `eShopPorted` project, which targets net461 and EF Core 2.2 and so doesn't count as a .NET 8 port.

Namespaces and type names are the same as in the legacy code (`eShopLegacyMVC.Models`, `eShopLegacyMVC.Services`,
`eShopLegacyMVC.ViewModel`, ...), so controllers can switch to the new library by changing only `using`/DI code.

## Build and test

```bash
cd eShopLegacyMVCSolution
dotnet build eShopLegacyMVC.Net8.sln
dotnet test tests/eShopLegacyMVC.Data.Tests            # no database needed

# Side-by-side parity tests: legacy EF6 code vs. the port, on a real SQL Server (tests are skipped if the variable is not set)
docker run -d --name eshop-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='<password>' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
ESHOP_TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=<password>;TrustServerCertificate=True" \
  dotnet test tests/eShopLegacyMVC.Data.ParityTests
```

`eShopLegacyMVC.Data.ParityTests` compiles the **unmodified legacy source files** (linked from `src/eShopLegacyMVC`)
with EF 6.5.1 on .NET 8, alongside the ported library, and checks these against SQL Server:

- The schema matches on columns (order, type, length, precision, nullability, identity), PKs, indexes, FKs and sequences.
- Seeding (default and `UseCustomizationData`) produces the same rows, keys and sequence positions, and extracts the same `Pics` files.
- The same scenario of controller operations (paging, find, create, update, delete, validation failures, FK violations,
  concurrency failures, detached deletes, conflicting graphs) gives the same results and leaves the same final database.
- Each layer works against a database the other one created.
- The HiLo generator stays unique when used from several threads.

## Usage in an ASP.NET Core host

```csharp
// appsettings.json: { "ConnectionStrings": { "CatalogDBContext": "..." }, "UseMockData": false, "UseCustomizationData": false }
builder.Services.AddCatalogData(builder.Configuration, o => o.ContentRootPath = builder.Environment.ContentRootPath);
var app = builder.Build();
app.Services.InitializeCatalogDatabase();   // replaces Database.SetInitializer(...) in Global.asax
```

## What ported cleanly

- Entities and data annotations are copied as-is. `PictureUri` is still ignored by the model.
- Mapping: table names, keys, required/max-length facets, `decimal(18,2)` price, required cascade FKs. Constraint
  and index names are set explicitly to EF6's conventions (`PK_dbo.Catalog`, `FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId`, `IX_CatalogBrandId`, ...), so the EF Core model matches existing databases.
- `CatalogService` has the same queries: `LongCount`, `Include`, `OrderBy(Id)`, `Skip/Take`, and `FirstOrDefault` in Find.
  `GetCatalogBrands`/`GetCatalogTypes` still return the live `DbSet` (deferred execution).
- `CatalogServiceMock`, `PreconfiguredData` and `PaginatedItemsViewModel` are copied verbatim.
- `CatalogItemHiLoGenerator` keeps the same algorithm: one `NEXT VALUE FOR catalog_hilo` per 10 ids, process-wide lock, `int` cast.
- The CSV/zip customization seed uses the same parsing, the same fallbacks to preconfigured data, and the same exceptions.

## What did not port cleanly, and how the port handles it

1. **Validate-on-save.** EF6 validated Added/Modified entities in `SaveChanges` and threw
   `System.Data.Entity.Validation.DbEntityValidationException`. EF Core doesn't validate. The port overrides
   `SaveChanges`/`SaveChangesAsync` to validate data annotations and the model's implicit Required/MaxLength facets, and throws a look-alike
   `eShopLegacyMVC.Models.Validation.DbEntityValidationException` (same message, `EntityValidationErrors`, `PropertyName`/`ErrorMessage`).
   `CatalogDBContext.ValidateOnSaveEnabled` stands in for `Configuration.ValidateOnSaveEnabled`. The exception is a *different type*,
   so any `catch (System.Data.Entity.Validation.DbEntityValidationException)` must be updated. The quirk where
   `[Range(0, 1000000)]` on a decimal accepts `1000000.01` is kept, because both layers use the same `RangeAttribute`.
2. **Database initializer.** `CreateDatabaseIfNotExists<T>` and `Database.SetInitializer` don't exist in EF Core.
   `CatalogDBInitializer.InitializeDatabase` uses `EnsureCreated()` and seeds only when it created the database.
   Differences: (a) EF Core writes no `__MigrationHistory` table and does no model-hash check, so EF6's
   "model backing the context has changed" exception is gone; (b) if the database exists but has no tables, `EnsureCreated` creates the schema and seeds,
   while EF6 did nothing and later queries failed. For production, I'd recommend EF Core migrations with a baseline
   migration for existing databases (see next steps).
3. **Sequence scripts.** The legacy seed ran `Models\Infrastructure\dbo.*_hilo.Sequence.sql` using a Windows-style path relative to
   `AppDomain.BaseDirectory`, and each script hard-codes `USE [Microsoft.eShopOnContainers.Services.CatalogDb]`. The port
   declares the three sequences in the model (`HasSequence<long>(...).StartsAt(1).IncrementsBy(10)`), so they are created with the schema and are
   no longer tied to one database name. The `.sql` files are no longer used.
4. **Brand/type ids.** EF6 treated `CatalogBrand.Id`/`CatalogType.Id` as IDENTITY and silently ignored the sequence values the
   seed assigned. EF Core would send those values and fail on IDENTITY_INSERT. The port doesn't set the ids, but it still consumes
   `catalog_brand_hilo`/`catalog_type_hilo` once each, so sequence positions and row ids come out the same.
5. **Detached delete.** EF6's `DbSet.Remove` threw `InvalidOperationException` ("...not found in the ObjectStateManager") for
   untracked entities. EF Core attaches the entity and deletes it. `CatalogService.RemoveCatalogItem` checks for this and throws the same exception.
6. **Conflicting FK/navigation on update.** EF6 threw "A referential integrity constraint violation occurred..." when an updated
   graph's `CatalogBrand.Id`/`CatalogType.Id` didn't match the FK. EF Core silently lets the FK win. `UpdateCatalogItem` checks for this and throws the same way.
   The MVC `Edit` action binds a navigation-less item, so the app itself never hits this case.
7. **Web.config / System.Web.** `ConfigurationManager.AppSettings` and `HostingEnvironment.ApplicationPhysicalPath` become
   `CatalogDataOptions` (`UseMockData`, `UseCustomizationData`, `ContentRootPath`), bound from `IConfiguration` with the same key
   names. The connection string is still named `CatalogDBContext`, but it is passed to `UseSqlServer` instead of `name=`. `Microsoft.Data.SqlClient`
   encrypts by default, so connection strings to dev servers usually need `TrustServerCertificate=True`. The legacy `(localdb)` string is Windows-only.
8. **Autofac / Global.asax.** `ApplicationModule` becomes `AddCatalogData`: the mock service is a singleton, the real service/context/initializer are scoped
   (was `InstancePerLifetimeScope`), and the HiLo generator is a singleton.
9. **Exception types.** `DbUpdateException`/`DbUpdateConcurrencyException` now come from `Microsoft.EntityFrameworkCore`, and
   `SqlException` from `Microsoft.Data.SqlClient`. The failures happen in the same cases (verified), but catch blocks need new namespaces.
10. **Async/sync.** The API stays synchronous to match `ICatalogService`. Async variants would be a follow-up.

## Assumptions

- "Catalog data-access layer" means the Models, Models/Infrastructure, Services and ViewModel folders. Controllers, views,
  logging (log4net), the MVC host and Docker files are out of scope, and the legacy app still runs on EF6.
- SQL Server remains the target provider. No migrations were generated. `EnsureCreated` matches the legacy create-if-missing behavior.
- Existing production databases created by EF6 must keep working without schema changes. The parity tests check this in both directions.
