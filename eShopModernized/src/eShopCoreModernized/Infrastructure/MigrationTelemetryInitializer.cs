using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace eShopCoreModernized.Infrastructure
{
    public class MigrationTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.GlobalProperties["ApplicationVersion"] = ".NET 8";
            telemetry.Context.GlobalProperties["MigrationPhase"] = "StranglerFig-Complete";
            telemetry.Context.GlobalProperties["ServiceType"] = "Catalog-Core";
        }
    }
}
