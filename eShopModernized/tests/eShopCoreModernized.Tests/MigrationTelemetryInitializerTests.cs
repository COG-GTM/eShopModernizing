using eShopCoreModernized.Infrastructure;
using Microsoft.ApplicationInsights.DataContracts;

namespace eShopCoreModernized.Tests;

public class MigrationTelemetryInitializerTests
{
    [Fact]
    public void Initialize_SetsExpectedGlobalProperties()
    {
        var initializer = new MigrationTelemetryInitializer();
        var telemetry = new RequestTelemetry();

        initializer.Initialize(telemetry);

        Assert.Equal(".NET Core 6.0", telemetry.Context.GlobalProperties["ApplicationVersion"]);
        Assert.Equal("StranglerFig-Complete", telemetry.Context.GlobalProperties["MigrationPhase"]);
        Assert.Equal("Catalog-Core", telemetry.Context.GlobalProperties["ServiceType"]);
    }
}
