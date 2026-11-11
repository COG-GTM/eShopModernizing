using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.WebForms.Hosting.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShop.Catalog.WebForms.Tests.S9_Ops;

/// <summary>Runs only when ESHOP_TEST_SQLSERVER holds a SQL Server connection string (e.g. the compose "sqldata" service).</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "ESHOP_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Set {EnvironmentVariable} to a SQL Server connection string to run.";
        }
    }
}

public class SqlServerReadinessTests
{
    [SqlServerFact]
    public async Task Database_check_is_healthy_against_a_live_sql_server()
    {
        var options = new CatalogDataOptions
        {
            UseMockData = false,
            ConnectionString = Environment.GetEnvironmentVariable(SqlServerFactAttribute.EnvironmentVariable),
        };
        var registration = new HealthCheckRegistration(CatalogDatabaseHealthCheck.Name, new CatalogDatabaseHealthCheck(options), null, null);

        var result = await new CatalogDatabaseHealthCheck(options).CheckHealthAsync(new HealthCheckContext { Registration = registration });

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
