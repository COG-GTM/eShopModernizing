using Microsoft.Extensions.Configuration;

namespace eShop.Catalog.Data.DependencyInjection;

/// <summary>Legacy appSettings "UseMockData" / "UseCustomizationData" and connection string "CatalogDBContext".</summary>
public sealed class CatalogDataOptions
{
    public const string ConnectionStringName = "CatalogDBContext";

    public bool UseMockData { get; init; }

    public bool UseCustomizationData { get; init; }

    public string? ConnectionString { get; init; }

    public static CatalogDataOptions FromConfiguration(IConfiguration configuration)
    {
        return new CatalogDataOptions
        {
            UseMockData = configuration.GetValue("UseMockData", true),
            UseCustomizationData = configuration.GetValue("UseCustomizationData", false),
            ConnectionString = configuration.GetConnectionString(ConnectionStringName),
        };
    }
}
