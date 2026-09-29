using System;

namespace eShopLegacyMVC.Configuration
{
    public class CatalogSettings : ICatalogSettings
    {
        public const string UseMockDataKey = "UseMockData";
        public const string UseCustomizationDataKey = "UseCustomizationData";

        private readonly IConfigurationReader reader;

        public CatalogSettings(IConfigurationReader reader)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public bool UseMockData => GetRequiredBoolean(UseMockDataKey);

        public bool UseCustomizationData => GetRequiredBoolean(UseCustomizationDataKey);

        private bool GetRequiredBoolean(string key)
        {
            return bool.Parse(reader.GetSetting(key));
        }
    }
}
