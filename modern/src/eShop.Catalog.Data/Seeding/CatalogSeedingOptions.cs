namespace eShop.Catalog.Data.Seeding;

/// <summary>
/// Where the UseCustomizationData seed reads its files. Defaults mirror the legacy app layout relative to the host
/// content root: <c>Setup/</c> (CatalogTypes.csv, CatalogBrands.csv, CatalogItems.csv, CatalogItems.zip) and <c>Pics/</c>.
/// </summary>
public sealed class CatalogSeedingOptions
{
    public string? SetupPath { get; set; }

    public string? PicturesPath { get; set; }
}
