using System.Data;
using eShop.Catalog.Data.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace eShop.Catalog.Data.Persistence;

/// <summary>
/// Applies the EF Core migrations and runs <see cref="CatalogSeeder"/> at startup (legacy CreateDatabaseIfNotExists
/// + Seed). Both hosts may share one database, so the whole step is serialised with an exclusive session-level
/// <c>sp_getapplock</c> taken in <c>master</c> on a non-pooled connection (the catalog database may not exist yet).
/// </summary>
public class CatalogDatabaseInitializer
{
    public const string AppLockResourcePrefix = "eShop.Catalog.Data:init:";

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<CatalogDatabaseInitializer> logger;

    public CatalogDatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<CatalogDatabaseInitializer>? logger = null)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger ?? NullLogger<CatalogDatabaseInitializer>.Instance;
    }

    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<CatalogSeeder>();

        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("CatalogDbContext has no connection string.");
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        var resource = AppLockResourcePrefix + database;

        await using var lockConnection = await OpenLockConnectionAsync(connectionString, cancellationToken);
        await AcquireAppLockAsync(lockConnection, resource, cancellationToken);
        try
        {
            logger.LogInformation("Applying catalog migrations to {Database}", database);
            await db.Database.MigrateAsync(cancellationToken);
            await seeder.SeedAsync(db, cancellationToken);
        }
        finally
        {
            await ReleaseAppLockAsync(lockConnection, resource);
        }
    }

    private async Task<SqlConnection> OpenLockConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", Pooling = false };
        var deadline = DateTime.UtcNow + ConnectTimeout;
        while (true)
        {
            var connection = new SqlConnection(builder.ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (SqlException ex) when (DateTime.UtcNow < deadline)
            {
                await connection.DisposeAsync();
                logger.LogWarning("SQL Server not reachable yet ({Message}); retrying", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    private async Task AcquireAppLockAsync(SqlConnection connection, string resource, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = (int)LockTimeout.TotalSeconds + 30;
        command.Parameters.AddWithValue("@Resource", resource);
        command.Parameters.AddWithValue("@LockMode", "Exclusive");
        command.Parameters.AddWithValue("@LockOwner", "Session");
        command.Parameters.AddWithValue("@LockTimeout", (int)LockTimeout.TotalMilliseconds);
        var result = command.Parameters.Add("@ReturnValue", SqlDbType.Int);
        result.Direction = ParameterDirection.ReturnValue;

        await command.ExecuteNonQueryAsync(cancellationToken);

        var status = (int)result.Value;
        if (status < 0)
        {
            throw new TimeoutException($"Could not acquire catalog initialisation lock '{resource}' (sp_getapplock returned {status}).");
        }
    }

    private async Task ReleaseAppLockAsync(SqlConnection connection, string resource)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "sp_releaseapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Resource", resource);
            command.Parameters.AddWithValue("@LockOwner", "Session");
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException ex)
        {
            // Closing the non-pooled session releases the lock anyway.
            logger.LogWarning(ex, "sp_releaseapplock failed for {Resource}", resource);
        }
    }
}

/// <summary>Runs <see cref="CatalogDatabaseInitializer"/> before the host starts serving requests.</summary>
public sealed class CatalogDatabaseInitializerHostedService : IHostedService
{
    private readonly CatalogDatabaseInitializer initializer;

    public CatalogDatabaseInitializerHostedService(CatalogDatabaseInitializer initializer)
    {
        this.initializer = initializer;
    }

    public Task StartAsync(CancellationToken cancellationToken) => initializer.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
