using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Mock;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Infrastructure.DependencyInjection
{
    // Owned by ticket T2 (services + DI + UseMockData toggle). The scaffold already wires the mock so the UI/API
    // tickets can replay transcripts from day one; T2 hardens it (lifetimes, logging, validation of the toggle).
    public static class CatalogServicesExtensions
    {
        /// <summary>Mock mode (UseMockData=true, the default): CatalogServiceMock as a singleton ICatalogService.</summary>
        public static IServiceCollection AddCatalogServices(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration.GetCatalogSettings().UseMockData)
            {
                services.AddSingleton<ICatalogService, CatalogServiceMock>();
            }
            return services;
        }
    }
}
