// FROZEN shared contract: legacy appSettings keys, bound from configuration (appsettings / env vars).
namespace EShop.Catalog.Contracts;

public class CatalogOptions
{
    /// <summary>Legacy appSetting <c>UseMockData</c>: in-memory singleton catalog instead of SQL Server.</summary>
    public bool UseMockData { get; set; } = true;

    /// <summary>Legacy appSetting <c>UseCustomizationData</c>: seed from Setup/*.csv + CatalogItems.zip.</summary>
    public bool UseCustomizationData { get; set; }

    public const string ConnectionStringName = "CatalogDBContext";
}
