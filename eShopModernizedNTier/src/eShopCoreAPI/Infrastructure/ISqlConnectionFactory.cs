using Microsoft.Data.SqlClient;

namespace eShopCoreAPI.Infrastructure;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}
