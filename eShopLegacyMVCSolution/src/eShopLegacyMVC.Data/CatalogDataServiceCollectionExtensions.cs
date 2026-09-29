using System;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Infrastructure;
using eShopLegacyMVC.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace eShopLegacyMVC
{
    /// <summary>
    /// Microsoft.Extensions.DependencyInjection replacement for the legacy Autofac ApplicationModule and the
    /// database-initializer wiring in Global.asax.cs.
    /// </summary>
    public static class CatalogDataServiceCollectionExtensions
    {
        public static IServiceCollection AddCatalogData(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<CatalogDataOptions> configure = null)
        {
            var options = new CatalogDataOptions();
            configuration.Bind(options);
            configure?.Invoke(options);

            services.AddSingleton(Options.Create(options));

            if (options.UseMockData)
            {
                services.AddSingleton<ICatalogService, CatalogServiceMock>();
            }
            else
            {
                services.AddScoped<ICatalogService, CatalogService>();
            }

            services.AddDbContext<CatalogDBContext>(dbOptions =>
            {
                var connectionString = configuration.GetConnectionString(CatalogDataOptions.ConnectionStringName);
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        $"Connection string '{CatalogDataOptions.ConnectionStringName}' is not configured.");
                }

                dbOptions.UseSqlServer(connectionString);
            });

            services.AddScoped<CatalogDBInitializer>();
            services.AddSingleton<CatalogItemHiLoGenerator>();

            return services;
        }

        /// <summary>
        /// Creates and seeds the catalog database if it does not exist yet, like the legacy
        /// Database.SetInitializer(CatalogDBInitializer) call. No-op when UseMockData is true.
        /// </summary>
        public static void InitializeCatalogDatabase(this IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var options = scope.ServiceProvider.GetRequiredService<IOptions<CatalogDataOptions>>().Value;
                if (options.UseMockData)
                {
                    return;
                }

                var context = scope.ServiceProvider.GetRequiredService<CatalogDBContext>();
                scope.ServiceProvider.GetRequiredService<CatalogDBInitializer>().InitializeDatabase(context);
            }
        }
    }
}
