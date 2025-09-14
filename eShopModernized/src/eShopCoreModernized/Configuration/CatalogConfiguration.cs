namespace eShopCoreModernized.Configuration
{
    public class CatalogConfiguration : ICatalogConfiguration
    {
        private readonly IConfiguration _configuration;

        public CatalogConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool UseMockData => GetBoolValue("AppSettings:UseMockData");

        public bool UseAzureStorage => GetBoolValue("AppSettings:UseAzureStorage");

        public bool UseManagedIdentity => GetBoolValue("AppSettings:UseAzureManagedIdentity");

        public bool UseCustomizationData => GetBoolValue("AppSettings:UseCustomizationData");

        public bool UseAzureActiveDirectory => GetBoolValue("AppSettings:UseAzureActiveDirectory");

        public string StorageConnectionString => _configuration["Azure:StorageConnectionString"] ?? string.Empty;

        public string AppInsightsInstrumentationKey => _configuration["Azure:ApplicationInsights:InstrumentationKey"] ?? string.Empty;

        public string AzureActiveDirectoryClientId => _configuration["Azure:ActiveDirectory:ClientId"] ?? string.Empty;

        public string AzureActiveDirectoryTenant => _configuration["Azure:ActiveDirectory:TenantId"] ?? string.Empty;

        public string PostLogoutRedirectUri => _configuration["Azure:ActiveDirectory:PostLogoutRedirectUri"] ?? string.Empty;

        private bool GetBoolValue(string key)
        {
            return _configuration.GetValue<bool>(key);
        }
    }
}
