using System.Collections.Generic;
using System.Linq;

namespace eShopLegacyMVC.Data.ParityTests
{
    /// <summary>
    /// Provider-level description of a catalog database read from the SQL Server catalog views, independent of the
    /// ORM that created it.
    /// </summary>
    internal static class DatabaseSnapshot
    {
        // EF6 keeps its model hash in __MigrationHistory; EF Core's EnsureCreated has no equivalent table.
        private const string UserTables = "t.name <> '__MigrationHistory'";

        public static List<string> Columns(string database) => SqlServer.Query(database, $@"
            SELECT s.name AS [schema], t.name AS [table], c.column_id AS ordinal, c.name AS [column], ty.name AS [type],
                   c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity,
                   CAST(ic.seed_value AS bigint) AS identity_seed, CAST(ic.increment_value AS bigint) AS identity_increment,
                   c.collation_name, dc.definition AS default_value
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.columns c ON c.object_id = t.object_id
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
            WHERE {UserTables}
            ORDER BY t.name, c.column_id");

        public static List<string> Indexes(string database) => SqlServer.Query(database, $@"
            SELECT t.name AS [table], i.name AS [index], i.type_desc, i.is_unique, i.is_primary_key,
                   STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS [columns]
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.type > 0 AND {UserTables}
            GROUP BY t.name, i.name, i.type_desc, i.is_unique, i.is_primary_key
            ORDER BY t.name, i.name");

        public static List<string> ForeignKeys(string database) => SqlServer.Query(database, @"
            SELECT fk.name, tp.name AS [table], cp.name AS [column], tr.name AS principal_table, cr.name AS principal_column,
                   fk.delete_referential_action_desc, fk.update_referential_action_desc, fk.is_disabled, fk.is_not_trusted
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.tables tp ON tp.object_id = fkc.parent_object_id
            JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
            JOIN sys.tables tr ON tr.object_id = fkc.referenced_object_id
            JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
            ORDER BY fk.name");

        public static List<string> Sequences(string database) => SqlServer.Query(database, @"
            SELECT SCHEMA_NAME(s.schema_id) AS [schema], s.name, TYPE_NAME(s.user_type_id) AS [type],
                   CAST(s.start_value AS bigint) AS start_value, CAST(s.increment AS bigint) AS increment,
                   CAST(s.minimum_value AS bigint) AS minimum_value, CAST(s.maximum_value AS bigint) AS maximum_value,
                   s.is_cycling, s.is_cached, s.cache_size
            FROM sys.sequences s
            ORDER BY s.name");

        public static List<string> SequenceValues(string database) => SqlServer.Query(database, @"
            SELECT s.name, CAST(s.current_value AS bigint) AS current_value FROM sys.sequences s ORDER BY s.name");

        public static List<string> Tables(string database) => SqlServer.Query(database,
            "SELECT t.name FROM sys.tables t ORDER BY t.name");

        public static List<string> Data(string database) =>
            SqlServer.Query(database, "SELECT * FROM [CatalogType] ORDER BY Id")
                .Concat(SqlServer.Query(database, "SELECT * FROM [CatalogBrand] ORDER BY Id"))
                .Concat(SqlServer.Query(database, "SELECT * FROM [Catalog] ORDER BY Id"))
                .ToList();

        public static List<string> Everything(string database) =>
            Columns(database).Concat(Indexes(database)).Concat(ForeignKeys(database)).Concat(Sequences(database))
                .Concat(SequenceValues(database)).Concat(Data(database)).ToList();
    }
}
