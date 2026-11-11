using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.Data.Persistence;
using eShop.Catalog.Data.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace eShop.Catalog.Data.Tests.S1_Data.Integration;

/// <summary>One real SQL Server 2022 container (Docker) per test run; each test uses its own empty database.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    public Task InitializeAsync() => container.StartAsync();

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    public string NewDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = "CatalogDb_" + Guid.NewGuid().ToString("N")[..12],
            TrustServerCertificate = true,
        }.ConnectionString;

    /// <summary>Builds the same container an app host builds via <c>AddCatalogData(configuration)</c>.</summary>
    public static ServiceProvider BuildApp(string connectionString, bool useCustomizationData = false)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["UseMockData"] = "false",
            ["UseCustomizationData"] = useCustomizationData.ToString(),
            ["ConnectionStrings:CatalogDBContext"] = connectionString,
        }).Build();

        return new ServiceCollection()
            .AddLogging()
            .AddCatalogData(configuration)
            .Configure<CatalogSeedingOptions>(o =>
            {
                o.SetupPath = TestSupport.NonExistentDirectory();
                o.PicturesPath = TestSupport.NonExistentDirectory();
            })
            .BuildServiceProvider();
    }

    /// <summary>Runs the registered startup hosted services, exactly as the generic host does on app start.</summary>
    public static async Task StartAsync(IServiceProvider app)
    {
        foreach (var hosted in app.GetServices<IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }
    }

    public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<List<string>> RowsAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return rows;
    }

    public static CatalogDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "S1 SQL Server";
}
