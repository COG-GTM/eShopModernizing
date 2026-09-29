using System.Text.Json;
using System.Text.Json.Serialization;
using eShopCoreModernized.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopCoreModernized.HealthChecks
{
    public static class HealthCheckResponseWriter
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static Task WriteAsync(HttpContext context, HealthReport report)
        {
            var deployment = context.RequestServices.GetRequiredService<DeploymentInfo>();
            context.Response.ContentType = "application/json; charset=utf-8";
            return context.Response.WriteAsync(JsonSerializer.Serialize(CreatePayload(report, deployment), SerializerOptions));
        }

        public static object CreatePayload(HealthReport report, DeploymentInfo deployment) => new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            timestamp = DateTimeOffset.UtcNow,
            service = deployment.ServiceName,
            version = deployment.Version,
            deploymentTrack = deployment.Track,
            instance = deployment.Instance,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                description = entry.Value.Description,
                tags = entry.Value.Tags,
                data = entry.Value.Data.Count > 0 ? entry.Value.Data : null,
            }),
        };
    }
}
