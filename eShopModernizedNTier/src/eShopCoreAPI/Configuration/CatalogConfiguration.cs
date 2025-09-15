namespace eShopCoreAPI.Configuration;

public class CatalogConfiguration : ICatalogConfiguration
{
    private readonly IConfiguration _configuration;

    public CatalogConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool UseMockData => GetBoolValue("FeatureFlags:UseMockData");

    public bool UseAzureStorage => GetBoolValue("FeatureFlags:UseAzureStorage");

    public bool UseManagedIdentity => GetBoolValue("FeatureFlags:UseAzureManagedIdentity");

    public bool UseCustomizationData => GetBoolValue("FeatureFlags:UseCustomizationData");

    public bool UseAzureActiveDirectory => GetBoolValue("FeatureFlags:UseAzureActiveDirectory");

    public string StorageConnectionString => _configuration["Azure:StorageConnectionString"] ?? string.Empty;

    public string AppInsightsInstrumentationKey => _configuration["ApplicationInsights:InstrumentationKey"] ?? string.Empty;

    public string AzureActiveDirectoryClientId => _configuration["Azure:ActiveDirectory:ClientId"] ?? string.Empty;

    public string AzureActiveDirectoryTenant => _configuration["Azure:ActiveDirectory:Tenant"] ?? string.Empty;

    public string PostLogoutRedirectUri => _configuration["Azure:ActiveDirectory:PostLogoutRedirectUri"] ?? string.Empty;

    private bool GetBoolValue(string key)
    {
        var value = _configuration[key];
        return bool.TryParse(value, out var result) && result;
    }
}
