using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eShop.Catalog.Infrastructure.Data
{
    // Owned by ticket T1 (data layer). Scaffold stub: replace the bodies, keep the signatures.
    public static class CatalogDataExtensions
    {
        /// <summary>SQL mode (UseMockData=false): register CatalogDBContext, HiLo generator, initializer and the EF ICatalogService.</summary>
        public static IServiceCollection AddCatalogData(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }

        /// <summary>SQL mode: create the database, HiLo sequences and seed data (legacy CatalogDBInitializer).</summary>
        public static Task InitializeCatalogDataAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
