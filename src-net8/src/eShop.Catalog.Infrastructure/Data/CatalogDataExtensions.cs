using eShop.Catalog.Core;
using eShop.Catalog.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eShop.Catalog.Infrastructure.Data
{
    public static class CatalogDataExtensions
    {
        /// <summary>SQL mode (UseMockData=false): register CatalogDBContext, HiLo generator, initializer and the EF ICatalogService.</summary>
        public static IServiceCollection AddCatalogData(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration.GetCatalogSettings().UseMockData)
            {
                return services;
            }

            // Lifetimes mirror the legacy Autofac ApplicationModule: context, initializer and service per scope
            // (InstancePerLifetimeScope), HiLo generator SingleInstance.
            services.AddDbContext<CatalogDBContext>((sp, options) =>
                options.UseSqlServer(GetConnectionString(sp.GetRequiredService<IConfiguration>())));
            services.AddSingleton<CatalogItemHiLoGenerator>();
            services.AddScoped<CatalogDBInitializer>();
            services.AddScoped<ICatalogService, CatalogService>();
            return services;
        }

        /// <summary>SQL mode: create the database, HiLo sequences and seed data (legacy CatalogDBInitializer).</summary>
        public static async Task InitializeCatalogDataAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            if (host.Services.GetRequiredService<IConfiguration>().GetCatalogSettings().UseMockData)
            {
                return;
            }

            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CatalogDBInitializer>().InitializeAsync(cancellationToken);
        }

        private static string GetConnectionString(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(CatalogSettings.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"UseMockData=false requires ConnectionStrings:{CatalogSettings.ConnectionStringName} (env ConnectionStrings__{CatalogSettings.ConnectionStringName}).");
            }
            return connectionString;
        }
    }
}
