# eShopModernized Tests

xUnit test suite for the .NET 8 `eShopCoreModernized` application.

## Running the tests

From the `eShopModernized/` directory (requires the .NET 8 SDK):

```bash
dotnet test
```

Or target the test project directly:

```bash
dotnet test tests/eShopModernized.Tests/eShopModernized.Tests.csproj
```

## Layout

- `eShopModernized.Tests/` — xUnit test project referencing
  `src/eShopCoreModernized`.
- `CatalogServiceMockTests.cs` — tests for the in-memory `CatalogServiceMock`,
  which needs no database and serves as an example for adding more tests.
