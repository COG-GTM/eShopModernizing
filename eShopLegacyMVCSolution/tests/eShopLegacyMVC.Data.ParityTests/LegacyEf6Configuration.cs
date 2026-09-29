using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;
using System.Data.SqlClient;

namespace eShopLegacyMVC.Data.ParityTests
{
    /// <summary>
    /// Code-based replacement for the &lt;entityFramework&gt; section of the legacy Web.config. EF6 discovers it
    /// automatically because it lives in the same assembly as the (linked) legacy CatalogDBContext.
    /// </summary>
    public class LegacyEf6Configuration : DbConfiguration
    {
        public LegacyEf6Configuration()
        {
            SetProviderFactory(SqlProviderServices.ProviderInvariantName, SqlClientFactory.Instance);
            SetProviderServices(SqlProviderServices.ProviderInvariantName, SqlProviderServices.Instance);
            SetDefaultConnectionFactory(new SqlConnectionFactory());
        }
    }
}
