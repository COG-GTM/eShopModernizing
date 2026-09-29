using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Data.SqlClient;
using Xunit;

namespace eShopLegacyMVC.Data.ParityTests
{
    internal static class SqlServer
    {
        public const string EnvironmentVariable = "ESHOP_TEST_SQLSERVER";

        // The legacy dbo.*_hilo.Sequence.sql scripts hard-code "USE [Microsoft.eShopOnContainers.Services.CatalogDb]",
        // so the legacy side must use exactly this database name.
        public const string LegacyDatabaseName = "Microsoft.eShopOnContainers.Services.CatalogDb";
        public const string PortedDatabaseName = "eShopPorted.Parity.CatalogDb";

        public static string BaseConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable);

        public static void SkipIfUnavailable() =>
            Skip.If(string.IsNullOrEmpty(BaseConnectionString), $"{EnvironmentVariable} is not set.");

        public static string ConnectionStringFor(string database) =>
            new SqlConnectionStringBuilder(BaseConnectionString) { InitialCatalog = database }.ConnectionString;

        public static string LegacyConnectionString
        {
            get
            {
                // System.Data.SqlClient (used by EF6) does not understand every Microsoft.Data.SqlClient keyword.
                var source = new SqlConnectionStringBuilder(BaseConnectionString);
                var builder = new System.Data.SqlClient.SqlConnectionStringBuilder
                {
                    DataSource = source.DataSource,
                    InitialCatalog = LegacyDatabaseName,
                    IntegratedSecurity = source.IntegratedSecurity,
                    TrustServerCertificate = source.TrustServerCertificate,
                    MultipleActiveResultSets = true,
                };
                if (!source.IntegratedSecurity)
                {
                    builder.UserID = source.UserID;
                    builder.Password = source.Password;
                }
                return builder.ConnectionString;
            }
        }

        public static void DropDatabase(string database)
        {
            System.Data.SqlClient.SqlConnection.ClearAllPools();
            SqlConnection.ClearAllPools();
            using var connection = new SqlConnection(ConnectionStringFor("master"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END";
            command.ExecuteNonQuery();
        }

        public static List<string> Query(string database, string sql)
        {
            using var connection = new SqlConnection(ConnectionStringFor(database));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new List<string>();
            while (reader.Read())
            {
                var row = new StringBuilder();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row.Append(i == 0 ? "" : " | ").Append(reader.GetName(i)).Append('=')
                        .Append(reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture));
                }
                rows.Add(row.ToString());
            }
            return rows;
        }

        public static string LegacySourceDirectory([CallerFilePath] string thisFile = null) =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", "..", "src", "eShopLegacyMVC"));
    }

    /// <summary>
    /// All tests share the two databases (and EF6's process-wide initializer/config state), so they run serially.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class SqlServerCollection
    {
        public const string Name = "SQL Server";
    }
}
