using eShop.Catalog.Mvc.Hosting.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;

namespace eShop.Catalog.Mvc.Hosting;

/// <summary>
/// Owned by slice S9 (health, logging). Replaces legacy log4net / Application Insights with ILogger JSON console
/// logging plus one request log line carrying the W3C trace id, and exposes liveness and readiness probes.
/// </summary>
public static class ObservabilityExtensions
{
    public const string HealthPath = "/healthz";
    public const string ReadinessPath = "/healthz/ready";
    public const string ReadyTag = "ready";

    public static WebApplicationBuilder AddCatalogObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.Configure(options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });

        builder.Services.AddTransient<IStartupFilter, RequestLoggingStartupFilter>();

        // Any check tagged "ready" (e.g. an EF Core DbContext check) is reported by /healthz/ready.
        builder.Services.AddHealthChecks()
            .AddCheck<CatalogDatabaseHealthCheck>(CatalogDatabaseHealthCheck.Name, tags: new[] { ReadyTag });

        return builder;
    }

    public static IEndpointRouteBuilder MapCatalogHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(HealthPath, new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks(ReadinessPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        return endpoints;
    }

    public static bool IsHealthProbe(PathString path) => path.StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase);

    private sealed class RequestLoggingStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseMiddleware<RequestLoggingMiddleware>();
                next(app);
            };
        }
    }
}
