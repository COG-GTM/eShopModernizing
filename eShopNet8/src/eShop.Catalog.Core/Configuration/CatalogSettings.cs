namespace eShop.Catalog.Core.Configuration;

// Binds the legacy appSettings keys. Section "Catalog" or root keys; env: Catalog__UseMockData / UseMockData, CLI: --UseMockData=false.
public class CatalogSettings
{
    public const string SectionName = "Catalog";
    public const string ConnectionStringName = "CatalogDBContext";

    public bool UseMockData { get; set; } = true;
    public bool UseCustomizationData { get; set; }
}
