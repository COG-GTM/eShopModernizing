using eShop.Catalog.Core.Configuration;
using eShop.Catalog.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.Catalog.Infrastructure.Data;

// OWNED BY T1 (EF Core data layer + HiLo + seeding).
public static class CatalogDataExtensions
{
    // UseMockData=false: CatalogDBContext (EF Core 8, SQL Server, HiLo sequences) and the EF CatalogService, both scoped
    // (legacy Autofac InstancePerLifetimeScope). UseMockData=true: nothing is registered (T2 registers the mock).
    public static IServiceCollection AddCatalogData(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetCatalogSettings().UseMockData)
        {
            return services;
        }

        var connectionString = configuration.GetConnectionString(CatalogSettings.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"UseMockData=false requires the connection string '{CatalogSettings.ConnectionStringName}' " +
                $"(ConnectionStrings:{CatalogSettings.ConnectionStringName} or env ConnectionStrings__{CatalogSettings.ConnectionStringName}).");
        }

        services.AddDbContext<CatalogDBContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ICatalogService, CatalogService>();
        services.TryAddSingleton<ICatalogItemSaveValidator, CatalogItemAnnotationsSaveValidator>();
        services.AddScoped<CatalogDBInitializer>();
        return services;
    }

    // Create/seed the database (legacy CatalogDBInitializer semantics) when UseMockData=false.
    public static async Task InitializeCatalogDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetService<CatalogDBInitializer>();
        if (initializer is null)
        {
            return;
        }

        await initializer.InitializeAsync(cancellationToken);
    }
}
