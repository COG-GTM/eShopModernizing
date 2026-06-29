# eShopCoreModernized Tests

Automated unit tests for the .NET 8 `eShopCoreModernized` project, written with
[xUnit](https://xunit.net/).

## Layout

- `eShopCoreModernized.Tests/` — xUnit test project referencing
  `../src/eShopCoreModernized/eShopModernized.csproj`.

The test project is included in the solution at
`eShopModernized/eShopModernized.sln`.

## Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

## Running the tests

From the `eShopModernized/` directory:

```bash
dotnet build eShopModernized.sln
dotnet test eShopModernized.sln
```

You can also run the test project directly:

```bash
dotnet test tests/eShopCoreModernized.Tests/eShopCoreModernized.Tests.csproj
```

## Collecting code coverage

Coverage is collected with [coverlet](https://github.com/coverlet-coverage/coverlet)
via the `coverlet.collector` package. From the `eShopModernized/` directory:

```bash
dotnet test eShopModernized.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

This produces a Cobertura report at
`TestResults/<guid>/coverage.cobertura.xml`. The `line-rate` attribute on the
`<package name="eShopCoreModernized">` element is the line coverage ratio for the
assembly (e.g. `0.1648` = 16.48%).

To turn the Cobertura XML into a human-readable HTML report, install
[ReportGenerator](https://github.com/danielpalme/ReportGenerator) and run:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/coverage-report" -reporttypes:Html
```
