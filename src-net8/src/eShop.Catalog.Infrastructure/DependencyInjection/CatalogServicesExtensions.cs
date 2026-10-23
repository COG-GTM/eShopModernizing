using eShop.Catalog.Core;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Mock;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.Infrastructure.DependencyInjection
{
    // Replaces the legacy Autofac ApplicationModule + the UseMockData read in Global.asax (RegisterContainer), and
    // log4net's root logger (log4Net.xml RollingFileAppender) with Microsoft.Extensions.Logging console output.
    public static class CatalogServicesExtensions
    {
        public const string MissingSqlCatalogServiceMessage =
            "UseMockData=false but no ICatalogService is registered: the SQL Server data layer (AddCatalogData) is not wired. " +
            "Set UseMockData=true (appsettings.json, env UseMockData=true or --UseMockData=true) to run on the in-memory mock catalog, " +
            "or register the EF Core ICatalogService in AddCatalogData before AddCatalogServices.";

        /// <summary>
        /// UseMockData=true (default): <see cref="CatalogServiceMock"/> as a singleton ICatalogService (legacy SingleInstance).
        /// UseMockData=false: nothing is registered here; the scoped EF Core service comes from AddCatalogData (legacy
        /// InstancePerLifetimeScope), and startup fails fast if it is missing. Either way the mode is logged at startup.
        /// </summary>
        public static IServiceCollection AddCatalogServices(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.GetCatalogSettings();

            if (settings.UseMockData)
            {
                services.RemoveAll<ICatalogService>();
                services.AddSingleton<ICatalogService, CatalogServiceMock>();
            }

            var registration = services.LastOrDefault(d => d.ServiceType == typeof(ICatalogService) && !d.IsKeyedService)
                ?? throw new InvalidOperationException(MissingSqlCatalogServiceMessage);

            services.AddLogging(logging => logging.AddConsole());
            services.RemoveAll<CatalogServiceMode>();
            services.AddSingleton(new CatalogServiceMode(settings.UseMockData, registration.Lifetime, DescribeImplementation(registration)));
            services.AddHostedService<CatalogModeStartupLogger>();

            return services;
        }

        private static string DescribeImplementation(ServiceDescriptor registration)
        {
            var type = registration.ImplementationType ?? registration.ImplementationInstance?.GetType();
            return type?.Name ?? nameof(ICatalogService) + " (factory)";
        }
    }
}
