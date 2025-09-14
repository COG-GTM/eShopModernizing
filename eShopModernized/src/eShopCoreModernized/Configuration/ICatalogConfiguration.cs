namespace eShopCoreModernized.Configuration
{
    public interface ICatalogConfiguration
    {
        bool UseMockData { get; }
        bool UseAzureStorage { get; }
        bool UseManagedIdentity { get; }
        bool UseCustomizationData { get; }
        bool UseAzureActiveDirectory { get; }
        string StorageConnectionString { get; }
        string AppInsightsInstrumentationKey { get; }
        string AppInsightsConnectionString { get; }
        string AzureActiveDirectoryClientId { get; }
        string AzureActiveDirectoryTenant { get; }
        string PostLogoutRedirectUri { get; }
    }
}
