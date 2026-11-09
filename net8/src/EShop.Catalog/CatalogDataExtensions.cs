using EShop.Catalog.Contracts;
using EShop.Catalog.Data;
using EShop.Catalog.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EShop.Catalog;

public static class CatalogDataExtensions
{
    /// <summary>
    /// Legacy Autofac <c>ApplicationModule</c>: <see cref="ICatalogService"/> is the <see cref="CatalogServiceMock"/> singleton when
    /// <c>UseMockData=true</c>, otherwise the scoped EF Core <see cref="CatalogService"/> (with a singleton HiLo generator).
    /// </summary>
    public static IServiceCollection AddCatalogData(this IServiceCollection services, IConfiguration configuration)
    {
        var useMockData = configuration.GetValue("UseMockData", true);
        services.Configure<CatalogOptions>(o =>
        {
            o.UseMockData = useMockData;
            o.UseCustomizationData = configuration.GetValue("UseCustomizationData", false);
        });

        if (useMockData)
        {
            services.AddSingleton<ICatalogService, CatalogServiceMock>();
            return services;
        }

        var connectionString = configuration.GetConnectionString(CatalogOptions.ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{CatalogOptions.ConnectionStringName} is required when UseMockData=false.");
        services.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(connectionString));
        services.AddSingleton<CatalogItemHiLoGenerator>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<CatalogDbInitializer>();
        return services;
    }

    /// <summary>Creates and seeds the SQL database when <c>UseMockData=false</c> (legacy <c>CatalogDBInitializer</c>); no-op in mock mode.</summary>
    public static async Task InitializeCatalogDataAsync(this IHost host)
    {
        if (host.Services.GetRequiredService<IOptions<CatalogOptions>>().Value.UseMockData)
        {
            return;
        }

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbInitializer>().InitializeAsync();
    }
}
