using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.HealthChecks;
using eShopCoreModernized.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopCoreModernized.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ICatalogConfiguration _config;
        private readonly HealthCheckService _healthCheckService;
        private readonly DeploymentInfo _deployment;

        public HealthController(
            ICatalogConfiguration config,
            HealthCheckService healthCheckService,
            DeploymentInfo deployment)
        {
            _config = config;
            _healthCheckService = healthCheckService;
            _deployment = deployment;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var report = await _healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains(HealthCheckTags.Ready),
                cancellationToken);

            return ToResult(report, new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                version = _deployment.Version,
                deploymentTrack = _deployment.Track,
            });
        }

        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailed(CancellationToken cancellationToken)
        {
            var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

            var services = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => (object)new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                });

            services["configuration"] = new
            {
                useMockData = _config.UseMockData,
                useAzureStorage = _config.UseAzureStorage,
                useManagedIdentity = _config.UseManagedIdentity,
                useAzureActiveDirectory = _config.UseAzureActiveDirectory
            };

            return ToResult(report, new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                version = _deployment.Version,
                deploymentTrack = _deployment.Track,
                instance = _deployment.Instance,
                services
            });
        }

        private ObjectResult ToResult(HealthReport report, object body) =>
            StatusCode(
                report.Status == HealthStatus.Unhealthy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK,
                body);
    }
}
