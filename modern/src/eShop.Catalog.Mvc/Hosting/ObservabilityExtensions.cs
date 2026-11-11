namespace eShop.Catalog.Mvc.Hosting;

/// <summary>Owned by slice S9 (health, logging). S0 ships the minimal /healthz liveness endpoint.</summary>
public static class ObservabilityExtensions
{
    public const string HealthPath = "/healthz";

    public static WebApplicationBuilder AddCatalogObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks();
        return builder;
    }

    public static IEndpointRouteBuilder MapCatalogHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(HealthPath);
        return endpoints;
    }
}
