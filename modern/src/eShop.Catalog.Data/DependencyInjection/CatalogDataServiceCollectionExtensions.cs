using eShop.Catalog.Data.Persistence;
using eShop.Catalog.Data.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Data.DependencyInjection;

public static class CatalogDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers ICatalogService for both hosts: the in-memory mock when UseMockData=true (legacy SingleInstance),
    /// otherwise the EF Core persistence registered by <see cref="CatalogPersistenceServiceCollectionExtensions"/> (slice S1).
    /// </summary>
    public static IServiceCollection AddCatalogData(this IServiceCollection services, IConfiguration configuration)
    {
        var options = CatalogDataOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        if (options.UseMockData)
        {
            services.AddSingleton<ICatalogService, CatalogServiceMock>();
            return services;
        }

        return services.AddCatalogPersistence(options);
    }
}
