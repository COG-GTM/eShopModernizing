using Microsoft.Data.SqlClient;

namespace eShopCoreAPI.Infrastructure;

public class AppSettingsSqlConnectionFactory : ISqlConnectionFactory
{
    private readonly IConfiguration _configuration;

    public AppSettingsSqlConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_configuration.GetConnectionString("CatalogDBContext"));
    }
}
