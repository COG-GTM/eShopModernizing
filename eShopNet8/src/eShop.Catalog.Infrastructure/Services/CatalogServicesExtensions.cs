using eShop.Catalog.Core.Configuration;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, CatalogServiceRegistrationCheck>());
        }

        return services;
    }

    // ILogger replacement for log4net: console provider, levels from the "Logging" section, and a per-request scope
    // carrying the legacy Application_BeginRequest properties (activityid, requestinfo).
    public static ILoggingBuilder AddCatalogLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.AddConfiguration(configuration.GetSection("Logging"));
        logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss,fff ";
        });
        logging.Services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, RequestLogScopeStartupFilter>());
        return logging;
    }
}
