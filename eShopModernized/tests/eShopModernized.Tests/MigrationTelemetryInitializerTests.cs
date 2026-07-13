using eShopCoreModernized.Infrastructure;
using Microsoft.ApplicationInsights.DataContracts;

namespace eShopModernized.Tests;

public class MigrationTelemetryInitializerTests
{
    [Fact]
    public void Initialize_SetsExpectedMigrationGlobalProperties()
    {
        var initializer = new MigrationTelemetryInitializer();
        var telemetry = new TraceTelemetry();

        initializer.Initialize(telemetry);

        Assert.Equal(".NET Core 6.0", telemetry.Context.GlobalProperties["ApplicationVersion"]);
        Assert.Equal("StranglerFig-Complete", telemetry.Context.GlobalProperties["MigrationPhase"]);
        Assert.Equal("Catalog-Core", telemetry.Context.GlobalProperties["ServiceType"]);
    }

    [Fact]
    public void Initialize_AddsExactlyTheExpectedProperties()
    {
        var initializer = new MigrationTelemetryInitializer();
        var telemetry = new RequestTelemetry();

        initializer.Initialize(telemetry);

        Assert.Equal(3, telemetry.Context.GlobalProperties.Count);
        Assert.Contains("ApplicationVersion", telemetry.Context.GlobalProperties.Keys);
        Assert.Contains("MigrationPhase", telemetry.Context.GlobalProperties.Keys);
        Assert.Contains("ServiceType", telemetry.Context.GlobalProperties.Keys);
    }

    [Fact]
    public void Initialize_OverwritesExistingMigrationProperties()
    {
        var initializer = new MigrationTelemetryInitializer();
        var telemetry = new TraceTelemetry();
        telemetry.Context.GlobalProperties["ApplicationVersion"] = "stale";

        initializer.Initialize(telemetry);

        Assert.Equal(".NET Core 6.0", telemetry.Context.GlobalProperties["ApplicationVersion"]);
    }
}
