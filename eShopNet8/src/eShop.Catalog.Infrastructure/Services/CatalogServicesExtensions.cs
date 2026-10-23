using eShop.Catalog.Core.Configuration;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.Infrastructure.Services;

// Replaces the legacy Autofac ApplicationModule (service registration) and log4net configuration.
public static class CatalogServicesExtensions
{
    // UseMockData=true: CatalogServiceMock singleton (legacy SingleInstance).
    // UseMockData=false: the EF CatalogService (scoped) is registered by AddCatalogData; a start-up check fails fast if it is missing.
    public static IServiceCollection AddCatalogServices(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetCatalogSettings();

        if (settings.UseMockData)
        {
            services.AddSingleton<ICatalogService, CatalogServiceMock>();
        }
        else
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CatalogServiceRegistrationCheck>());
        }

        return services;
    }

    // ILogger replacement for log4net: console provider with per-request scopes (see CatalogLoggingDefaults);
    // levels and formatter options come from the "Logging" section, which overrides the code defaults.
    public static ILoggingBuilder AddCatalogLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.AddConfiguration(new ConfigurationBuilder().AddInMemoryCollection(CatalogLoggingDefaults.Values).Build());
        logging.AddConfiguration(configuration.GetSection("Logging"));
        logging.Configure(options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId | ActivityTrackingOptions.ParentId);
        return logging;
    }
}
