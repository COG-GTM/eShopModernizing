namespace eShop.Catalog.Core
{
    /// <summary>Configuration contract shared by both web apps (see migration/CONTRACT.md).</summary>
    public class CatalogSettings
    {
        public const string UseMockDataKey = "UseMockData";
        public const string UseCustomizationDataKey = "UseCustomizationData";
        public const string ConnectionStringName = "CatalogDBContext";

        /// <summary>true = in-memory singleton mock catalog (legacy CatalogServiceMock); false = SQL Server via EF Core.</summary>
        public bool UseMockData { get; set; } = true;

        /// <summary>Seed SQL from Setup/*.csv + CatalogItems.zip instead of PreconfiguredData (SQL mode only).</summary>
        public bool UseCustomizationData { get; set; }
    }
}
