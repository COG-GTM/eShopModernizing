using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace eShopModernized.Tests.TestSupport
{
    /// <summary>
    /// A minimal <see cref="DbConnection"/> whose commands return a configurable
    /// integer from <see cref="DbCommand.ExecuteScalar"/>. Used to drive
    /// <c>CatalogItemHiLoGenerator</c> without a real SQL Server sequence.
    /// </summary>
    internal sealed class FakeSequenceConnection : DbConnection
    {
        private readonly int _scalarValue;
        private ConnectionState _state = ConnectionState.Closed;

        public int OpenCount { get; private set; }
        public int ExecuteScalarCount { get; private set; }
        public string? LastCommandText { get; private set; }

        public FakeSequenceConnection(int scalarValue)
        {
            _scalarValue = scalarValue;
        }

        [AllowNull]
        public override string ConnectionString { get; set; } = "DataSource=:memory:";
        public override string Database => "fake";
        public override string DataSource => ":memory:";
        public override string ServerVersion => "0.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;

        public override void Open()
        {
            OpenCount++;
            _state = ConnectionState.Open;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new FakeCommand(this);

        private object OnExecuteScalar(string? commandText)
        {
            ExecuteScalarCount++;
            LastCommandText = commandText;
            return _scalarValue;
        }

        private sealed class FakeCommand : DbCommand
        {
            private readonly FakeSequenceConnection _owner;

            public FakeCommand(FakeSequenceConnection owner) => _owner = owner;

            [AllowNull]
            public override string CommandText { get; set; } = string.Empty;
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            [AllowNull]
            protected override DbConnection? DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel() { }
            public override int ExecuteNonQuery() => 0;
            public override object ExecuteScalar() => _owner.OnExecuteScalar(CommandText);
            public override void Prepare() { }
            protected override DbParameter CreateDbParameter() => throw new NotSupportedException();
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
                => throw new NotSupportedException();
        }
    }
}
