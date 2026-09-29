using eShopCoreModernized.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopCoreModernized.HealthChecks
{
    public sealed partial class HealthStatusLogPublisher : IHealthCheckPublisher
    {
        private readonly ILogger<HealthStatusLogPublisher> _logger;
        private readonly DeploymentInfo _deployment;
        private HealthStatus? _lastStatus;

        public HealthStatusLogPublisher(ILogger<HealthStatusLogPublisher> logger, DeploymentInfo deployment)
        {
            _logger = logger;
            _deployment = deployment;
        }

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            using var scope = _logger.BeginScope(LogScopeState.ForDeployment(_deployment));

            foreach (var (name, entry) in report.Entries)
            {
                if (entry.Status != HealthStatus.Healthy)
                {
                    LogCheckNotHealthy(_logger, entry.Exception, name, entry.Status, entry.Description ?? string.Empty, entry.Duration.TotalMilliseconds);
                }
            }

            var previous = _lastStatus;
            _lastStatus = report.Status;
            var level = previous != report.Status
                ? report.Status == HealthStatus.Healthy ? LogLevel.Information : LogLevel.Warning
                : LogLevel.Debug;

            LogReport(_logger, level, report.Status, previous?.ToString() ?? "Unknown", report.Entries.Count, report.TotalDuration.TotalMilliseconds);
            return Task.CompletedTask;
        }

        [LoggerMessage(
            EventId = 2000,
            EventName = "HealthReport",
            Message = "Readiness status {HealthStatus} (previous {PreviousHealthStatus}) across {HealthCheckCount} checks in {ElapsedMs:0.0} ms")]
        private static partial void LogReport(ILogger logger, LogLevel level, HealthStatus healthStatus, string previousHealthStatus, int healthCheckCount, double elapsedMs);

        [LoggerMessage(
            EventId = 2001,
            EventName = "HealthCheckNotHealthy",
            Level = LogLevel.Warning,
            Message = "Health check {HealthCheckName} reported {HealthStatus}: {HealthCheckDescription} ({ElapsedMs:0.0} ms)")]
        private static partial void LogCheckNotHealthy(ILogger logger, Exception? exception, string healthCheckName, HealthStatus healthStatus, string healthCheckDescription, double elapsedMs);
    }
}
