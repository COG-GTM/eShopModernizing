using eShop.Catalog.Core;
using Microsoft.Extensions.Configuration;

namespace eShop.Catalog.Infrastructure
{
    public static class CatalogConfiguration
    {
        /// <summary>
        /// Reads the top-level UseMockData / UseCustomizationData keys (appsettings.json, then environment
        /// variables UseMockData=false etc., then command line). Missing keys keep the CatalogSettings defaults.
        /// </summary>
        public static CatalogSettings GetCatalogSettings(this IConfiguration configuration)
        {
            var settings = new CatalogSettings();
            settings.UseMockData = configuration.GetValue(CatalogSettings.UseMockDataKey, settings.UseMockData);
            settings.UseCustomizationData = configuration.GetValue(CatalogSettings.UseCustomizationDataKey, settings.UseCustomizationData);
            return settings;
        }
    }
}
