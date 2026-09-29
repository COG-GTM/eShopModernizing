using eShopCoreModernized.Configuration;
using eShopCoreModernized.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopCoreModernized.HealthChecks
{
    public static class HealthCheckExtensions
    {
        public const string LivenessPath = "/health/live";
        public const string ReadinessPath = "/health/ready";
        public const string FullReportPath = "/health";
        public const string DetailedReportPath = "/health/detailed";

        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PublishPeriod = TimeSpan.FromSeconds(30);

        public static IServiceCollection AddCatalogHealthChecks(this IServiceCollection services, ICatalogConfiguration catalogConfig)
        {
            var builder = services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy("Process is running"), tags: new[] { HealthCheckTags.Live });

            if (catalogConfig.UseMockData)
            {
                builder.AddCheck(
                    "catalog-data",
                    () => HealthCheckResult.Healthy("Serving in-memory mock catalog data"),
                    tags: new[] { HealthCheckTags.Ready });
            }
            else
            {
                builder.AddDbContextCheck<CatalogDBContext>(
                    "catalog-db",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { HealthCheckTags.Ready });
            }

            if (catalogConfig.UseAzureStorage)
            {
                builder.AddCheck<BlobStorageHealthCheck>(
                    "blob-storage",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { HealthCheckTags.Ready });
            }

            services.Configure<HealthCheckServiceOptions>(options =>
            {
                foreach (var registration in options.Registrations)
                {
                    if (registration.Timeout == Timeout.InfiniteTimeSpan)
                    {
                        registration.Timeout = CheckTimeout;
                    }
                }
            });

            services.AddSingleton<IHealthCheckPublisher, HealthStatusLogPublisher>();
            services.Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Period = PublishPeriod;
                options.Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready);
            });

            return services;
        }

        public static IEndpointRouteBuilder MapCatalogHealthChecks(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHealthChecks(LivenessPath, CreateOptions(registration => registration.Tags.Contains(HealthCheckTags.Live)));
            endpoints.MapHealthChecks(ReadinessPath, CreateOptions(registration => registration.Tags.Contains(HealthCheckTags.Ready)));
            endpoints.MapHealthChecks(FullReportPath, CreateOptions(_ => true));
            endpoints.MapHealthChecks(DetailedReportPath, CreateOptions(_ => true));
            return endpoints;
        }

        private static HealthCheckOptions CreateOptions(Func<HealthCheckRegistration, bool> predicate) => new()
        {
            Predicate = predicate,
            ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        };
    }
}
