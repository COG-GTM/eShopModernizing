using eShopCoreModernized.Infrastructure;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Channel;
using Moq;
using Xunit;

namespace eShopModernized.Tests.Infrastructure;

public class MigrationTelemetryInitializerTests
{
    [Fact]
    public void Initialize_AddsMigrationGlobalProperties()
    {
        var context = new TelemetryContext();
        var telemetry = new Mock<ITelemetry>();
        telemetry.SetupGet(t => t.Context).Returns(context);

        new MigrationTelemetryInitializer().Initialize(telemetry.Object);

        Assert.Equal(".NET Core 6.0", context.GlobalProperties["ApplicationVersion"]);
        Assert.Equal("StranglerFig-Complete", context.GlobalProperties["MigrationPhase"]);
        Assert.Equal("Catalog-Core", context.GlobalProperties["ServiceType"]);
    }
}
