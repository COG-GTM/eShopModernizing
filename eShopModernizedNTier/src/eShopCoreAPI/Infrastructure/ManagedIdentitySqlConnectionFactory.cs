using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace eShopCoreAPI.Infrastructure;

public class ManagedIdentitySqlConnectionFactory : ISqlConnectionFactory
{
    private readonly DefaultAzureCredential _credential;
    private readonly IConfiguration _configuration;

    public ManagedIdentitySqlConnectionFactory(IConfiguration configuration)
    {
        _credential = new DefaultAzureCredential();
        _configuration = configuration;
    }

    public SqlConnection CreateConnection()
    {
        var connection = new SqlConnection(_configuration.GetConnectionString("CatalogDBContext"));
        
        var accessToken = _credential.GetToken(
            new TokenRequestContext(new[] { "https://database.windows.net/.default" }));
        
        connection.AccessToken = accessToken.Token;
        
        return connection;
    }
}
