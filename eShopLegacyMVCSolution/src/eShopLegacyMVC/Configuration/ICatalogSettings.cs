namespace eShopLegacyMVC.Configuration
{
    /// <summary>
    /// Strongly-typed settings consumed by the catalog application.
    /// </summary>
    public interface ICatalogSettings
    {
        bool UseMockData { get; }

        bool UseCustomizationData { get; }
    }
}
