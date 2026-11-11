using eShop.Catalog.Data.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Data.Persistence;

/// <summary>Owned by slice S1 (EF Core 8 persistence). Until S1 lands only UseMockData=true is supported.</summary>
public static class CatalogPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogPersistence(this IServiceCollection services, CatalogDataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        throw new InvalidOperationException(
            "UseMockData=false requires the EF Core persistence from slice S1, which is not on this branch yet. Set UseMockData=true.");
    }
}
