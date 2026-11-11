# modern/ — eShop catalog on ASP.NET Core 8 (MECCA-style commerce catalog modernisation)

Target of epic **MBA-3836**: port the legacy .NET Framework 4.7.2 catalog apps
(`eShopLegacyMVCSolution` = ASP.NET MVC 5, `eShopLegacyWebFormsSolution` = WebForms + EF6) to
ASP.NET Core 8 / EF Core 8 on Linux containers, with behaviour parity against the legacy apps.
All modern code lives here; nothing outside `modern/` is modified by the programme.

## Layout

```
modern/
├── eShop.Catalog.Modern.sln
├── global.json                 SDK 8.0.4xx (rollForward latestFeature)
├── Directory.Build.props       net8.0, nullable, implicit usings, warnings as errors
├── Directory.Packages.props    central package versions (PackageReference has no Version)
├── src/
│   ├── eShop.Catalog.Data/     shared class library used by both hosts
│   │   ├── Models/             CatalogItem / CatalogBrand / CatalogType (legacy POCOs + annotations)
│   │   ├── ViewModel/          PaginatedItemsViewModel<T> (legacy shape)
│   │   ├── Services/           ICatalogService (legacy contract) + CatalogServiceMock (+ EF service from S1)
│   │   ├── Seeding/            PreconfiguredData: the legacy 12 items / 5 brands / 4 types
│   │   ├── Persistence/        EF Core 8 DbContext, mappings, HiLo, AddCatalogPersistence (S1)
│   │   └── DependencyInjection/ AddCatalogData(config): UseMockData switch (legacy setting names)
│   ├── eShop.Catalog.Mvc/      ASP.NET Core 8 MVC port of eShopLegacyMVC
│   │   ├── Program.cs          one line per slice, calls Hosting/* extension methods
│   │   ├── Hosting/            one extension file per owning slice
│   │   ├── Controllers/, Controllers/Api/, Views/Catalog/, Views/Shared/, wwwroot/
│   └── eShop.Catalog.WebForms/ ASP.NET Core 8 Razor Pages port of eShopLegacyWebForms
│       ├── Program.cs, Hosting/
│       └── Pages/, Pages/Catalog/, Pages/Shared/, wwwroot/
├── tests/
│   ├── eShop.Catalog.Data.Tests/      S0_Scaffold/, S1_Data/
│   ├── eShop.Catalog.Mvc.Tests/       S0_Scaffold/, S2_MvcHost/, S3_MvcRead/, S4_MvcWrite/, S5_MvcWebApi/, S9_Ops/
│   └── eShop.Catalog.WebForms.Tests/  S0_Scaffold/, S6_PagesHost/, S7_PagesRead/, S8_PagesWrite/, S9_Ops/
├── parity/                     legacy-on-Mono runner + normalised HTTP baseline (see parity/LEGACY-RUN.md)
└── deploy/                     Dockerfiles, compose (S9)
```

## Build, test, run (Linux)

```bash
cd modern
dotnet build eShop.Catalog.Modern.sln
dotnet test  eShop.Catalog.Modern.sln

# run a host (mock data by default: appsettings.json "UseMockData": true)
dotnet run --project src/eShop.Catalog.Mvc        # http://localhost:5200, GET /healthz -> 200 "Healthy"
dotnet run --project src/eShop.Catalog.WebForms   # http://localhost:5201, GET /healthz -> 200 "Healthy"

# legacy apps on Mono + xsp4 and the parity baseline
parity/run-legacy.sh            # MVC :5100, WebForms :5101, writes parity/baseline/mock/
```

When backgrounding a host from a non-interactive shell use
`(cd dir && exec setsid nohup dotnet X.dll --contentRoot $PWD) > log 2>&1 < /dev/null &`.

## Conventions

- **Parity first.** Status codes, redirects, visible text, validation messages, form field names, CSS classes and
  JSON shape match legacy (`parity/baseline/` is the reference). The only intentional hardenings are: missing
  antiforgery token -> 400 with no data change; text fields rendered HTML-encoded; `PictureFileName` cannot be
  over-posted / path-traversed; `/api/files` returns JSON instead of BinaryFormatter. Anything else is a defect.
- **Configuration keeps legacy names**: `UseMockData`, `UseCustomizationData`, connection string `CatalogDBContext`.
- **Program.cs is append-only**: each host's `Program.cs` calls one well-named extension method per concern
  (`AddCatalogMvcUi`, `MapCatalogWebApi`, ...). A slice changes the body of *its* `Hosting/*.cs` file, not
  `Program.cs`. If a new call is really needed, add one line next to the owning slice's lines.
- **Packages**: add `<PackageVersion>` lines to `Directory.Packages.props` (sorted, in a labelled group) and versionless
  `<PackageReference>` in the project. Do not bump versions other slices use without saying so in the PR.
- **Solution file**: S0 already contains every project; slices should not need to touch `eShop.Catalog.Modern.sln`.
- **Tests**: xUnit; host tests use `WebApplicationFactory<Program>` (`public partial class Program` exists in both
  hosts). Each slice puts its tests in the `S<n>_<Name>/` folder of the matching test project, namespace
  `eShop.Catalog.<Project>.Tests.S<n>_<Name>`.
- **Static files**: legacy has case-only duplicates (`images`/`Images`, `site.css`/`Site.css`). Keep ONE copy in
  `wwwroot` and serve both casings with a case-insensitive file provider; duplicates make `Microsoft.NET.Sdk.Web`
  fail with "Conflicting assets".
- Warnings are errors; keep `Nullable` enabled.

## Folder ownership (parallel slices)

| Slice | Owns (add/edit freely) | Shared touch points (minimal, additive) |
|---|---|---|
| S0 scaffold + baseline | `*.sln`, `global.json`, `Directory.*.props`, `README.md`, `parity/`, `src/eShop.Catalog.Data/{Models,ViewModel,DependencyInjection}`, `tests/*/S0_Scaffold/` | — |
| S1 data library (EF Core 8) | `src/eShop.Catalog.Data/Persistence/`, `Seeding/`, `Services/`, `tests/eShop.Catalog.Data.Tests/S1_Data/` | `Directory.Packages.props` (EF Core), `Data.csproj` package refs, `DependencyInjection/` call into `AddCatalogPersistence` |
| S2 MVC host/layout/static | `src/eShop.Catalog.Mvc/Hosting/MvcUiExtensions.cs`, `Views/Shared/`, `Views/_ViewStart.cshtml`, `Views/_ViewImports.cshtml`, `wwwroot/`, `tests/eShop.Catalog.Mvc.Tests/S2_MvcHost/` | — |
| S3 MVC read flows | `Controllers/CatalogController.cs` (Index, Details), `Controllers/PicController.cs`, `Views/Catalog/Index.cshtml`, `Details.cshtml`, `Hosting/MvcRoutingExtensions.cs`, `tests/…Mvc.Tests/S3_MvcRead/` | — |
| S4 MVC write flows | Create/Edit/Delete actions (as a `partial class CatalogController` in `Controllers/CatalogController.Write.cs`), `Views/Catalog/Create.cshtml`, `Edit.cshtml`, `Delete.cshtml`, `tests/…Mvc.Tests/S4_MvcWrite/` | — |
| S5 MVC Web API | `Controllers/Api/` (`/api/brands`, `/api/files`, `/api`), `Hosting/WebApiExtensions.cs`, `tests/…Mvc.Tests/S5_MvcWebApi/` | — |
| S6 Razor Pages host/layout/static/About/Contact | `src/eShop.Catalog.WebForms/Hosting/PagesUiExtensions.cs`, `Pages/Shared/`, `Pages/_ViewStart.cshtml`, `Pages/_ViewImports.cshtml`, `Pages/About.cshtml`, `Pages/Contact.cshtml`, `wwwroot/`, `tests/…WebForms.Tests/S6_PagesHost/` | — |
| S7 Razor Pages list/details/delete | `Pages/Index.cshtml` (Default.aspx list + paging), `Pages/Catalog/Details.cshtml`, `Pages/Catalog/Delete.cshtml`, `Hosting/PagesRoutingExtensions.cs`, `tests/…WebForms.Tests/S7_PagesRead/` | — |
| S8 Razor Pages create/edit | `Pages/Catalog/Create.cshtml`, `Pages/Catalog/Edit.cshtml`, `tests/…WebForms.Tests/S8_PagesWrite/` | `Hosting/PagesRoutingExtensions.cs` (add Create/Edit route conventions only) |
| S9 Dockerfiles/compose/health/logging | `deploy/`, `src/*/Hosting/ObservabilityExtensions.cs`, `.dockerignore`, `tests/*/S9_Ops/` | `appsettings*.json` logging sections |

Every host's `Program.cs` is annotated with the owning slice per line. Empty folders contain a `.gitkeep`; delete it
when you add the first real file.

## Legacy baseline

`parity/run-legacy.sh` builds both legacy apps from a scratch copy, runs them under Mono 6.12 + xsp4 and records
`parity/baseline/<mode>/<app>/NN-<route>.txt` (status, key headers, normalised body) plus `SUMMARY.tsv`.
`mock` (in-memory `CatalogServiceMock`) is the default; `--sql` runs EF6 against SQL Server 2022 in Docker.
Recipe, patches and known Mono-only differences: [`parity/LEGACY-RUN.md`](parity/LEGACY-RUN.md).
