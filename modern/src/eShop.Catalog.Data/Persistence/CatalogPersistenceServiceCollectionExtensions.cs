using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.Data.Seeding;
using eShop.Catalog.Data.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Data.Persistence;

/// <summary>Slice S1: EF Core 8 persistence used when UseMockData=false.</summary>
public static class CatalogPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogPersistence(this IServiceCollection services, CatalogDataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        services.AddDbContext<CatalogDbContext>(db => db.UseSqlServer(options.ConnectionString));
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddOptions<CatalogSeedingOptions>();
        services.AddScoped<CatalogSeeder>();
        services.AddSingleton<CatalogDatabaseInitializer>();
        services.AddHostedService<CatalogDatabaseInitializerHostedService>();
        return services;
    }

    /// <summary>Fails fast at startup when SQL mode is selected without a usable "CatalogDBContext" connection string.</summary>
    public static void Validate(CatalogDataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.UseMockData)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"UseMockData=false requires the connection string 'ConnectionStrings:{CatalogDataOptions.ConnectionStringName}'.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(options.ConnectionString);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
        {
            throw new InvalidOperationException(
                $"Connection string '{CatalogDataOptions.ConnectionStringName}' is not a valid SQL Server connection string: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException(
                $"Connection string '{CatalogDataOptions.ConnectionStringName}' must set both a server (Data Source/Server) and a database (Initial Catalog/Database).");
        }
    }
}
