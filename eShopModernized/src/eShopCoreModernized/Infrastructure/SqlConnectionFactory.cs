using Microsoft.Data.SqlClient;
using Azure.Identity;
using Azure.Core;

namespace eShopCoreModernized.Infrastructure
{
    public interface ISqlConnectionFactory
    {
        SqlConnection CreateConnection();
    }

    public class ManagedIdentitySqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly IConfiguration _configuration;
        private readonly DefaultAzureCredential _credential;

        public ManagedIdentitySqlConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
            _credential = new DefaultAzureCredential();
        }

        public SqlConnection CreateConnection()
        {
            var connectionString = _configuration.GetConnectionString("CatalogDBContext");
            var connection = new SqlConnection(connectionString);
            
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
            var token = _credential.GetToken(tokenRequestContext);
            connection.AccessToken = token.Token;
            
            return connection;
        }
    }

    public class AppSettingsSqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly IConfiguration _configuration;

        public AppSettingsSqlConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public SqlConnection CreateConnection()
        {
            var connectionString = _configuration.GetConnectionString("CatalogDBContext");
            return new SqlConnection(connectionString);
        }
    }
}
