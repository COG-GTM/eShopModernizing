using eShop.Catalog.Data.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShop.Catalog.Mvc.Hosting.Observability;

/// <summary>Readiness: SQL Server answers <c>SELECT 1</c> on the CatalogDBContext connection (always ready in mock mode).</summary>
public sealed class CatalogDatabaseHealthCheck : IHealthCheck
{
    public const string Name = "catalog-db";
    private const int MaxConnectTimeoutSeconds = 5;

    private readonly CatalogDataOptions options;

    public CatalogDatabaseHealthCheck(CatalogDataOptions options)
    {
        this.options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (options.UseMockData)
        {
            return HealthCheckResult.Healthy("UseMockData=true: no database dependency.");
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Connection string 'CatalogDBContext' is not configured.");
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(options.ConnectionString) { Pooling = false };
            builder.ConnectTimeout = Math.Clamp(builder.ConnectTimeout, 1, MaxConnectTimeoutSeconds);

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = MaxConnectTimeoutSeconds;
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("SQL Server reachable.");
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "SQL Server unreachable.", ex);
        }
    }
}
