using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace eShopCoreModernized.Infrastructure
{
    public class MigrationTelemetryInitializer : ITelemetryInitializer
    {
        private readonly DeploymentInfo _deployment;

        public MigrationTelemetryInitializer(DeploymentInfo deployment)
        {
            _deployment = deployment;
        }

        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.GlobalProperties["ApplicationVersion"] = _deployment.Version;
            telemetry.Context.GlobalProperties["DeploymentTrack"] = _deployment.Track;
            telemetry.Context.GlobalProperties["MigrationPhase"] = "StranglerFig-Complete";
            telemetry.Context.GlobalProperties["ServiceType"] = "Catalog-Core";
            telemetry.Context.Component.Version = _deployment.Version;
            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleName = _deployment.ServiceName;
            }
        }
    }
}
