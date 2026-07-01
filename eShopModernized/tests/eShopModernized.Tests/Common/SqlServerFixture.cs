using System.Text.RegularExpressions;
using eShopCoreModernized.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eShopModernized.Tests.Common;

/// <summary>
/// xUnit fixture that provisions a throw-away SQL Server database for the tests
/// that exercise SQL-Server-specific code paths (the HiLo sequence used by
/// <c>CatalogItemHiLoGenerator</c> and <c>CatalogService.CreateCatalogItem</c>).
///
/// These paths cannot run on the EF Core in-memory or SQLite providers because
/// they issue the T-SQL statement <c>SELECT NEXT VALUE FOR catalog_hilo</c>.
///
/// If no SQL Server is reachable the fixture reports <see cref="Available"/> =
/// <c>false</c> and the dependent tests skip cleanly, so <c>dotnet test</c>
/// still passes in environments without a database. Point the tests at a server
/// by setting the <c>TEST_SQL_CONNECTION</c> environment variable; otherwise a
/// local instance on <c>localhost,1433</c> is assumed.
/// </summary>
public sealed class SqlServerFixture : IDisposable
{
    private const string SequenceName = "catalog_hilo";

    public bool Available { get; }
    public string? SkipReason { get; }

    private readonly string? _databaseName;
    private readonly string? _masterConnectionString;
    private readonly string? _databaseConnectionString;

    public SqlServerFixture()
    {
        var baseConnection = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION")
            ?? "Server=127.0.0.1,1433;User Id=sa;Password=Str0ng!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5";

        try
        {
            var masterBuilder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" };
            _masterConnectionString = masterBuilder.ConnectionString;

            using (var connection = new SqlConnection(_masterConnectionString))
            {
                connection.Open();
            }

            _databaseName = $"eShopTest_{Guid.NewGuid():N}";
            var dbBuilder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = _databaseName };
            _databaseConnectionString = dbBuilder.ConnectionString;

            CreateDatabase();
            InitializeSchema();

            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            SkipReason = $"SQL Server not available: {ex.Message}";
        }
    }

    public CatalogDBContext CreateContext()
    {
        if (_databaseConnectionString is null)
        {
            throw new InvalidOperationException("SQL Server fixture is not available.");
        }

        var options = new DbContextOptionsBuilder<CatalogDBContext>()
            .UseSqlServer(_databaseConnectionString)
            .Options;

        return new CatalogDBContext(options);
    }

    private void CreateDatabase()
    {
        ExecuteMaster($"CREATE DATABASE [{_databaseName}];");
    }

    private void InitializeSchema()
    {
        using var context = CreateContext();

        // The EF create script marks int primary keys as IDENTITY. The real
        // catalog schema instead assigns CatalogItem ids from the HiLo sequence,
        // so strip IDENTITY to allow explicit id inserts on the Catalog table
        // (and to let the seed data below use explicit brand/type ids).
        var script = context.Database.GenerateCreateScript();
        script = Regex.Replace(script, @"\s+IDENTITY(\(\d+,\s*\d+\))?", string.Empty);

        foreach (var batch in SplitSqlBatches(script))
        {
            context.Database.ExecuteSqlRaw(batch);
        }

        // The HiLo generator relies on a SQL Server sequence that is not part of
        // the EF model, so create it explicitly.
        context.Database.ExecuteSqlRaw(
            $"CREATE SEQUENCE {SequenceName} AS int START WITH 1 INCREMENT BY 1;");

        context.CatalogBrands.AddRange(TestData.Brands());
        context.CatalogTypes.AddRange(TestData.Types());
        context.SaveChanges();
    }

    private static IEnumerable<string> SplitSqlBatches(string script)
    {
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                yield return batch;
            }
        }
    }

    private void ExecuteMaster(string sql)
    {
        using var connection = new SqlConnection(_masterConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (!Available || _databaseName is null)
        {
            return;
        }

        try
        {
            ExecuteMaster(
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}];");
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
