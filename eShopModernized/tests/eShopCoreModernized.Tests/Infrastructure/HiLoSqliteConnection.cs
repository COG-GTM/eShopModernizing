using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace eShopCoreModernized.Tests.Infrastructure
{
    /// <summary>
    /// In-memory SQLite connection that emulates the SQL Server <c>catalog_hilo</c> sequence
    /// (START WITH 1, INCREMENT BY 10), which SQLite cannot express natively.
    /// </summary>
    public sealed class HiLoSqliteConnection : SqliteConnection
    {
        public const string NextHiLoValueSql = "SELECT NEXT VALUE FOR catalog_hilo;";
        public const int SequenceStart = 1;
        public const int SequenceIncrement = 10;

        private long nextSequenceValue = SequenceStart;
        private int sequenceRequests;

        public HiLoSqliteConnection()
            : base("DataSource=:memory:")
        {
        }

        public int SequenceRequests => sequenceRequests;

        protected override DbCommand CreateDbCommand()
        {
            using var template = base.CreateCommand();
            return new HiLoSqliteCommand(this)
            {
                Connection = this,
                CommandTimeout = template.CommandTimeout,
                Transaction = template.Transaction,
            };
        }

        internal long NextSequenceValue()
        {
            Interlocked.Increment(ref sequenceRequests);
            return Interlocked.Add(ref nextSequenceValue, SequenceIncrement) - SequenceIncrement;
        }

        private sealed class HiLoSqliteCommand : SqliteCommand
        {
            private readonly HiLoSqliteConnection owner;

            public HiLoSqliteCommand(HiLoSqliteConnection owner)
            {
                this.owner = owner;
            }

            public override object? ExecuteScalar()
            {
                if (string.Equals(CommandText?.Trim(), NextHiLoValueSql, StringComparison.OrdinalIgnoreCase))
                {
                    return owner.NextSequenceValue();
                }

                return base.ExecuteScalar();
            }
        }
    }
}
