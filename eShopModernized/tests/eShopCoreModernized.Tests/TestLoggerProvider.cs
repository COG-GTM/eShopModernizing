using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace eShopCoreModernized.Tests
{
    public sealed record CapturedLog(
        string Category,
        LogLevel Level,
        EventId EventId,
        string Message,
        IReadOnlyDictionary<string, object?> State,
        IReadOnlyDictionary<string, object?> Scope,
        Exception? Exception);

    public sealed class TestLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

        public ConcurrentQueue<CapturedLog> Logs { get; } = new();

        public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

        public void Dispose()
        {
        }

        private static Dictionary<string, object?> ToDictionary(object? state)
        {
            var result = new Dictionary<string, object?>();
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                {
                    result[pair.Key] = pair.Value;
                }
            }
            return result;
        }

        private sealed class TestLogger : ILogger
        {
            private readonly string _category;
            private readonly TestLoggerProvider _provider;

            public TestLogger(string category, TestLoggerProvider provider)
            {
                _category = category;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                _provider._scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var scope = new Dictionary<string, object?>();
                _provider._scopeProvider.ForEachScope(
                    (scopeState, target) =>
                    {
                        foreach (var pair in ToDictionary(scopeState))
                        {
                            target[pair.Key] = pair.Value;
                        }
                    },
                    scope);

                _provider.Logs.Enqueue(new CapturedLog(
                    _category, logLevel, eventId, formatter(state, exception), ToDictionary(state), scope, exception));
            }
        }
    }
}
