# Migrating clients from the WCF `CatalogService` to the Catalog minimal API

`eShopLegacyNTier/src/eShopWCFService` (WCF, .NET Framework 4.x, EF6) has been replaced by
`eShopLegacyNTier/src/eShopCatalogApi`, an ASP.NET Core 8 minimal API that exposes the same
ten operations of the old `ICatalogService` contract over JSON/HTTP.

| Project | Target | Purpose |
|---|---|---|
| `src/eShopCatalogApi` | `net8.0` | Minimal API host (EF Core + SQL Server, same `eShopDatabase` schema) |
| `src/eShopCatalog.Contracts` | `netstandard2.0` | Shared DTOs (`CatalogItem`, `CatalogBrand`, `CatalogType`, `CatalogItemsStock`, `DiscountItem`) and route constants |
| `src/eShopCatalogApi.Client` | `netstandard2.0` | Drop-in replacement for the "Add Service Reference" proxy (`CatalogServiceClient`) |
| `tests/eShopCatalogApi.Tests` | `net8.0` | Integration tests (API + client over `WebApplicationFactory`, SQLite in-memory) |

## What stays the same

- **Operations and semantics.** Every operation keeps its name, parameters and behaviour:
  `0` (or omitted) brand/type filter means "all", stock and discount lookups compare by calendar
  date, `CreateAvailableStock` overwrites an existing entry for the same item/date, new ids are
  `max(id) + 1`, and "not found" lookups return `null` to the caller.
- **Data shapes.** DTOs keep the WCF `DataMember` names and JSON is emitted in **PascalCase**
  (`{"Id":1,"Name":"...","CatalogBrandId":2,...}`), so any hand-written JSON/XML mapping code
  keeps the same field names.
- **Database.** Same tables (`CatalogItems`, `CatalogBrands`, `CatalogTypes`, `CatalogItemsStock`,
  `DiscountItems`) and seed data. The connection string is read from `ConnectionString`
  (env var / config, as the WCF `Web.config` override did) and falls back to
  `ConnectionStrings:EntityModel`. Set `Catalog:InitializeDatabase=false` (env `Catalog__InitializeDatabase=false`) to skip `EnsureCreated` + seeding.
- **Port.** The dev launch profile still listens on `http://localhost:62314`.

## What changes

- Transport is REST/JSON instead of SOAP. There is no WSDL/MEX, and `CatalogService.svc`
  no longer exists. The contract is described by OpenAPI at `/swagger/v1/swagger.json`
  (Swagger UI at `/swagger` in Development); each operation's `operationId` is the WCF
  operation name.
- Errors are HTTP status codes (+ RFC 7807 problem details) instead of SOAP faults. The .NET client
  turns them into `CatalogServiceException` (`StatusCode`, `ResponseBody`) instead of
  `FaultException`/`CommunicationException`.
- `UpdateCatalogItem` / `RemoveCatalogItem` on a missing id now surface as `404`
  (WCF/EF6 threw a generic fault). `RemoveCatalogItem` only needs the item `Id`.
- `CreateCatalogItem` now returns the created item (`201` + `Location`), and the .NET client
  copies the server-assigned `Id` back onto the object you passed in.
- A `/health` endpoint (checks DB connectivity) is available for containers/orchestrators.

## Operation mapping

Routes are relative to the API root (e.g. `http://localhost:62314/`). Dates are sent as
`yyyy-MM-dd` in query strings.

| WCF operation | HTTP | Success | Not found |
|---|---|---|---|
| `FindCatalogItem(int id)` | `GET api/catalog/items/{id}` | `200` item (with `CatalogBrand`/`CatalogType`) | `404` → `null` |
| `GetCatalogBrands()` | `GET api/catalog/brands` | `200` array | – |
| `GetCatalogItems(int? brandIdFilter, int? typeIdFilter)` | `GET api/catalog/items?brandIdFilter=&typeIdFilter=` | `200` array | – |
| `GetCatalogTypes()` | `GET api/catalog/types` | `200` array | – |
| `GetAvailableStock(DateTime date, int catalogItemId)` | `GET api/catalog/items/{catalogItemId}/stock?date=` | `200` bare integer (`0` if none) | – |
| `CreateAvailableStock(CatalogItemsStock)` | `POST api/catalog/stock` | `204` | – |
| `CreateCatalogItem(CatalogItem)` | `POST api/catalog/items` | `201` created item | – |
| `UpdateCatalogItem(CatalogItem)` | `PUT api/catalog/items/{id}` (body `Id` must match, else `400`) | `204` | `404` |
| `RemoveCatalogItem(CatalogItem)` | `DELETE api/catalog/items/{id}` | `204` | `404` |
| `GetDiscount(DateTime day)` | `GET api/catalog/discount?day=` | `200` discount | `404` → `null` |

Example:

```http
GET /api/catalog/items/1/stock?date=2017-09-20      -> 200  100
GET /api/catalog/discount?day=2017-09-19            -> 200  {"Size":0.3,"Start":"2017-09-18T00:00:00","End":"2017-09-21T00:00:00","Id":1}
POST /api/catalog/stock
Content-Type: application/json
{"CatalogItemId":1,"Date":"2017-09-20T00:00:00","AvailableStock":7}   -> 204
```

## Migrating a .NET client (e.g. `eShopWinForms`)

`eShopCatalogApi.Client.ICatalogService` has the same member list as the interface generated
by "Add Service Reference" (sync + `*Async` pair per operation, array return types,
`int?` filters), so migration is mostly mechanical.

1. **Reference the new client.** It targets `netstandard2.0`, so it works from .NET Framework
   4.7.2+ (recommended; 4.6.1 works with the usual netstandard shims) and from modern .NET.
   `eShopWinForms` currently targets 4.7 — retarget it to 4.7.2 first, then add a project
   reference to `src/eShopCatalogApi.Client` (or consume it as a NuGet package).
2. **Remove the service reference.** Delete `Connected Services/eShopServiceReference`, and the
   `<system.serviceModel>` section from `App.config`. Drop the `System.ServiceModel` and
   `System.Runtime.Serialization` references if nothing else uses them.
3. **Swap namespaces.** Replace `using eShopWinForms.eShopServiceReference;` with
   ```csharp
   using eShopCatalog.Contracts;    // CatalogItem, CatalogBrand, ...
   using eShopCatalogApi.Client;    // ICatalogService, CatalogServiceClient
   ```
   Type and member names are unchanged, so views/controllers compile as-is.
4. **Configure the endpoint.** Replace the `<client><endpoint address=".../CatalogService.svc">`
   with an app setting and construct the client from it:
   ```xml
   <appSettings>
     <add key="CatalogApiBaseUrl" value="http://localhost:62314/" />
   </appSettings>
   ```
   ```csharp
   // Program.cs
   CatalogView catalogView = new CatalogView();
   var baseUrl = new Uri(ConfigurationManager.AppSettings["CatalogApiBaseUrl"]);
   using (var service = new CatalogServiceClient(baseUrl))   // was: new eShopServiceReference.CatalogServiceClient()
   {
       CatalogController catalogController = new CatalogController(service, catalogView);
       catalogController.LoadView();
       catalogView.ShowDialog();
   }
   ```
   In apps with DI (`IHttpClientFactory`), register a typed client instead:
   `services.AddHttpClient<ICatalogService, CatalogServiceClient>(c => c.BaseAddress = new Uri(baseUrl));`
5. **Update error handling.** Replace `catch (FaultException)` / `catch (CommunicationException)`
   with `catch (CatalogServiceException ex)` (inspect `ex.StatusCode`) and
   `catch (HttpRequestException)` for connectivity failures. Timeouts are controlled by
   `HttpClient.Timeout` rather than `sendTimeout`/`receiveTimeout` binding settings.
6. **Prefer the async methods from UI code.** The sync wrappers are safe to call from a
   WinForms UI thread (the client uses `ConfigureAwait(false)` throughout) but they block it;
   `await service.GetCatalogItemsAsync(...)` keeps the UI responsive.
7. **Behavioural checks.** Code that relied on a fault when updating/removing a missing item
   now gets a `CatalogServiceException` with `StatusCode == 404`. Code that re-queried to find
   the id of a newly created item can read `catalogItem.Id` directly after `CreateCatalogItem`.

### Incremental / side-by-side option

If the WinForms app can't be changed at the same time as the server, keep the old WCF service
deployed (it is still available in git history and in `eShopModernizedNTier`) against the same
database while clients move over — both hosts use the same schema and id-generation rules.
Switch clients one at a time by flipping `CatalogApiBaseUrl`, then retire the WCF host.

## Migrating a non-.NET client

Generate a client from `/swagger/v1/swagger.json` (e.g. `openapi-generator`, NSwag, Kiota),
or call the routes above directly. Remember that `0`/omitted filters mean "all", dates in query
strings are `yyyy-MM-dd`, and `404` from `items/{id}` and `discount` means "no result".

## Running locally

```bash
cd eShopLegacyNTier
# SQL Server via env var (or edit appsettings.json; default is (localdb)\MSSQLLocalDB)
export ConnectionString="Server=localhost,1433;Database=eShopDatabase;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
dotnet run --project src/eShopCatalogApi          # http://localhost:62314/swagger
dotnet test                                       # integration tests, no SQL Server needed
```
