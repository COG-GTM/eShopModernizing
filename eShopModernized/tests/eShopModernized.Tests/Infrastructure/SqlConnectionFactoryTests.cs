using eShopCoreModernized.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace eShopModernized.Tests.Infrastructure
{
    public class SqlConnectionFactoryTests
    {
        private static IConfiguration BuildConfiguration(string? connectionString)
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogDBContext"] = connectionString,
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }

        [Fact]
        public void AppSettings_CreateConnection_ReturnsSqlConnectionWithConfiguredConnectionString()
        {
            const string connectionString = "Server=tcp:test.database.windows.net;Database=Catalog;User Id=sa;Password=Pass@word1;";
            var factory = new AppSettingsSqlConnectionFactory(BuildConfiguration(connectionString));

            using var connection = factory.CreateConnection();

            Assert.IsType<SqlConnection>(connection);
            Assert.Equal(connectionString, connection.ConnectionString);
        }

        [Fact]
        public void AppSettings_CreateConnection_DoesNotOpenConnection()
        {
            var factory = new AppSettingsSqlConnectionFactory(
                BuildConfiguration("Server=tcp:test.database.windows.net;Database=Catalog;User Id=sa;Password=Pass@word1;"));

            using var connection = factory.CreateConnection();

            Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        }

        [Fact]
        public void AppSettings_CreateConnection_WithMissingConnectionString_ReturnsEmptyConnectionString()
        {
            var factory = new AppSettingsSqlConnectionFactory(BuildConfiguration(null));

            using var connection = factory.CreateConnection();

            Assert.IsType<SqlConnection>(connection);
            Assert.Equal(string.Empty, connection.ConnectionString);
        }

        [Fact]
        public void ManagedIdentity_Constructor_DoesNotThrow()
        {
            var factory = new ManagedIdentitySqlConnectionFactory(
                BuildConfiguration("Server=tcp:test.database.windows.net;Database=Catalog;"));

            Assert.NotNull(factory);
        }

        [Fact]
        public void ManagedIdentity_CreateConnection_ThrowsWithoutAzureCredentials()
        {
            var factory = new ManagedIdentitySqlConnectionFactory(
                BuildConfiguration("Server=tcp:test.database.windows.net;Database=Catalog;"));

            Assert.ThrowsAny<Exception>(() => factory.CreateConnection());
        }
    }
}
