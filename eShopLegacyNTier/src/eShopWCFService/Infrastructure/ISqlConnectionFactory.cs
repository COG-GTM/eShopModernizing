using Microsoft.Data.SqlClient;

namespace eShopWCFService.Infrastructure;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}
