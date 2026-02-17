using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace eShopCoreModernized.Infrastructure
{
    public class MigrationTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.GlobalProperties["ApplicationVersion"] = ".NET Core 6.0";
            telemetry.Context.GlobalProperties["MigrationPhase"] = "Phase4-FullMigration";
            telemetry.Context.GlobalProperties["ServiceType"] = "FullApp-Core";
            telemetry.Context.GlobalProperties["MigratedRoutes"] = "API,Catalog,Account,Home,UploadImage,StaticFiles,CatchAll";
        }
    }
}
