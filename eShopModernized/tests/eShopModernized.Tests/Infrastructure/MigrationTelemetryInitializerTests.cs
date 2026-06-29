using eShopCoreModernized.Infrastructure;
using Microsoft.ApplicationInsights.DataContracts;
using Xunit;

namespace eShopModernized.Tests.Infrastructure
{
    public class MigrationTelemetryInitializerTests
    {
        [Fact]
        public void Initialize_SetsMigrationGlobalProperties()
        {
            var initializer = new MigrationTelemetryInitializer();
            var telemetry = new TraceTelemetry();

            initializer.Initialize(telemetry);

            var properties = telemetry.Context.GlobalProperties;
            Assert.Equal(".NET Core 6.0", properties["ApplicationVersion"]);
            Assert.Equal("StranglerFig-Complete", properties["MigrationPhase"]);
            Assert.Equal("Catalog-Core", properties["ServiceType"]);
        }
    }
}
