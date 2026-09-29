namespace eShopLegacyMVC.Configuration
{
    /// <summary>
    /// Raw key/value access to application configuration. Mirrors the shape of
    /// Microsoft.Extensions.Configuration.IConfiguration (indexer + GetConnectionString)
    /// so an IConfiguration-backed implementation can replace the Web.config one.
    /// </summary>
    public interface IConfigurationReader
    {
        /// <summary>Returns the value for <paramref name="key"/>, or null when it is not defined.</summary>
        string GetSetting(string key);

        /// <summary>Returns the connection string named <paramref name="name"/>, or null when it is not defined.</summary>
        string GetConnectionString(string name);
    }
}
