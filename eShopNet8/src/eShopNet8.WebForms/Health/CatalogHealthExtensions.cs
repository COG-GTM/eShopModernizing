using System.Text.Json;
using eShop.Catalog.Core.Configuration;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace eShopNet8.WebForms.Health;

// OWNED BY T6 (containers, compose, CI, health endpoints).
// /health        liveness: the process answers; runs no checks.
// /health/ready  readiness: ICatalogService resolves; in SQL mode (UseMockData=false) the EF DbContext can connect.
// Neither probe lists the catalog, so probing never warms mock navigation properties (INVENTORY Q4, CONTRACT section 7).
public static class CatalogHealthExtensions
{
    public const string LivenessPath = "/health";
    public const string ReadinessPath = "/health/ready";
    private const string ReadyTag = "ready";

    public static IServiceCollection AddCatalogHealth(this IServiceCollection services, IConfiguration configuration)
    {
        var checks = services.AddHealthChecks()
            .AddCheck<CatalogServiceHealthCheck>("catalog-service", tags: new[] { ReadyTag });
        if (!configuration.GetCatalogSettings().UseMockData)
        {
            checks.AddCheck<CatalogDbContextHealthCheck>("catalog-db", tags: new[] { ReadyTag });
        }
        return services;
    }

    public static WebApplication MapCatalogHealth(this WebApplication app)
    {
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReadinessAsync,
        });
        return app;
    }

    // Status per check only: exception details stay in the log, never in the response.
    private static Task WriteReadinessAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), description = e.Value.Description }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}

internal sealed class CatalogServiceHealthCheck(IServiceProvider services) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var service = services.GetService<ICatalogService>();
        return Task.FromResult(service is null
            ? HealthCheckResult.Unhealthy("ICatalogService is not registered")
            : HealthCheckResult.Healthy(service.GetType().Name));
    }
}

// Equivalent to AddDbContextCheck<TContext>() without a compile-time dependency on the T1 context type:
// AddDbContext registers the non-generic DbContextOptions whose ContextType names the registered context.
internal sealed class CatalogDbContextHealthCheck(IServiceProvider services, ILogger<CatalogDbContextHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = services.GetService<DbContextOptions>();
        if (options is null || services.GetService(options.ContextType) is not DbContext db)
        {
            return HealthCheckResult.Unhealthy("no EF Core DbContext registered (UseMockData=false)");
        }
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy(options.ContextType.Name)
                : HealthCheckResult.Unhealthy(options.ContextType.Name + " cannot connect");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Readiness check failed for {Context}", options.ContextType.Name);
            return HealthCheckResult.Unhealthy(options.ContextType.Name + " cannot connect");
        }
    }
}
