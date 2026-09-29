using System.Configuration;

namespace eShopLegacyMVC.Configuration
{
    /// <summary>
    /// Reads appSettings and connectionStrings from Web.config via ConfigurationManager.
    /// </summary>
    public class WebConfigConfigurationReader : IConfigurationReader
    {
        public string GetSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public string GetConnectionString(string name)
        {
            return ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
        }
    }
}
