// Owned by T1 (catalog data). Scaffold stub: replace with real registrations.
using EShop.Catalog.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EShop.Catalog;

public static class CatalogDataExtensions
{
    /// <summary>Registers <see cref="ICatalogService"/> (mock singleton or EF Core scoped) per <c>UseMockData</c>.</summary>
    public static IServiceCollection AddCatalogData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CatalogOptions>(o =>
        {
            o.UseMockData = configuration.GetValue("UseMockData", true);
            o.UseCustomizationData = configuration.GetValue("UseCustomizationData", false);
        });
        return services;
    }

    /// <summary>Creates/seeds the SQL database when <c>UseMockData=false</c> (legacy CatalogDBInitializer). No-op in the stub.</summary>
    public static Task InitializeCatalogDataAsync(this IHost host) => Task.CompletedTask;
}
