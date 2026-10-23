using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.Infrastructure.Services;

// OWNED BY T2 (service layer, mock, DI, UseMockData toggle, logging). Scaffold stub: no ICatalogService registered yet.
public static class CatalogServicesExtensions
{
    // Register ICatalogService: CatalogServiceMock (singleton) when UseMockData=true, EF CatalogService (scoped) otherwise.
    public static IServiceCollection AddCatalogServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    // ILogger configuration replacing log4net.
    public static ILoggingBuilder AddCatalogLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        return logging;
    }
}
