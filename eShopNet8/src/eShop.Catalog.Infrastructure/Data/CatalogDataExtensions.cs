using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Infrastructure.Data;

// OWNED BY T1 (EF Core data layer + HiLo + seeding). Scaffold stub: SQL mode is not wired yet.
public static class CatalogDataExtensions
{
    // Register CatalogDBContext (EF Core 8, SQL Server, HiLo sequences) when UseMockData=false.
    public static IServiceCollection AddCatalogData(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    // Create/seed the database (legacy CatalogDBInitializer semantics) when UseMockData=false.
    public static Task InitializeCatalogDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
