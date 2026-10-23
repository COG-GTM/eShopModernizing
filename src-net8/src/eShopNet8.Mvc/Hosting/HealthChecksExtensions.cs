using eShop.Catalog.Core;
using eShop.Catalog.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopNet8.Mvc.Hosting
{
    // Owned by ticket T6 (containerisation). GET /health: 200 "Healthy" when the process is live and, with
    // UseMockData=false, the catalog database answers; 503 "Unhealthy" otherwise (the container healthcheck probes it).
    public static class HealthChecksExtensions
    {
        public const string HealthPath = "/health";

        public static IServiceCollection AddCatalogHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var checks = services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

            if (!configuration.GetCatalogSettings().UseMockData)
            {
                checks.AddCheck<CatalogSqlHealthCheck>(
                    "catalog-sql",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "ready", "sql" },
                    timeout: TimeSpan.FromSeconds(10));
            }

            return services;
        }

        public static WebApplication MapCatalogHealthChecks(this WebApplication app)
        {
            app.MapHealthChecks(HealthPath);
            return app;
        }
    }

    internal sealed class CatalogSqlHealthCheck : IHealthCheck
    {
        private readonly IConfiguration configuration;

        public CatalogSqlHealthCheck(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var connectionString = configuration.GetConnectionString(CatalogSettings.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return new HealthCheckResult(context.Registration.FailureStatus,
                    $"ConnectionStrings:{CatalogSettings.ConnectionStringName} is not configured");
            }

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy($"SQL Server reachable ({builder.DataSource}/{builder.InitialCatalog})");
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server unreachable", ex);
            }
        }
    }
}
