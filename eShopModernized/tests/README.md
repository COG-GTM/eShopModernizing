# eShopModernized Tests

Unit tests for the modernized .NET 8 catalog service (`eShopModernized/src/eShopCoreModernized`).

## Stack
- [xUnit](https://xunit.net/) — test framework
- [Moq](https://github.com/devlooped/moq) — mocking dependencies
- [coverlet](https://github.com/coverlet-coverage/coverlet) — code coverage (`XPlat Code Coverage`)

## Layout
Tests mirror the source folder structure under `eShopModernized.Tests/`:

| Source | Tests |
| --- | --- |
| `Services/CatalogServiceMock.cs` | `Services/CatalogServiceMockTests.cs` |
| `Services/ImageMockStorage.cs` | `Services/ImageMockStorageTests.cs` |
| `Controllers/BrandsController.cs` | `Controllers/BrandsControllerTests.cs` |
| `Controllers/CatalogController.cs` | `Controllers/CatalogControllerTests.cs` |
| `Configuration/CatalogConfiguration.cs` | `Configuration/CatalogConfigurationTests.cs` |
| `ViewModel/PaginatedItemsViewModel.cs` | `ViewModel/PaginatedItemsViewModelTests.cs` |
| `Models/*` | `Models/CatalogModelsTests.cs` |
| `Infrastructure/MigrationTelemetryInitializer.cs` | `Infrastructure/MigrationTelemetryInitializerTests.cs` |

## Running

```bash
# from repo root
dotnet test eShopModernized/tests/eShopModernized.Tests/eShopModernized.Tests.csproj

# with coverage (produces TestResults/<guid>/coverage.cobertura.xml)
dotnet test eShopModernized/tests/eShopModernized.Tests/eShopModernized.Tests.csproj \
  --collect:"XPlat Code Coverage"
```

Tests are hermetic — no SQL Server, Azure, or network access is required. External
boundaries (catalog data, image storage, configuration, telemetry) are mocked or use
the in-memory mock implementations.
