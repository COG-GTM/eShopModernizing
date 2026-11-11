using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.WebForms.Tests.S9_Ops;

public sealed record CapturedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> State,
    IReadOnlyDictionary<string, object?> Scope)
{
    public object? Get(string key) => State.TryGetValue(key, out var value) ? value : null;
}

/// <summary>In-memory ILoggerProvider capturing structured state and active scopes.</summary>
public sealed class InMemoryLogSink : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> entries = new();
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

    public IReadOnlyList<CapturedLog> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new SinkLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    public async Task<IReadOnlyList<CapturedLog>> WaitForAsync(Func<CapturedLog, bool> predicate, int expected = 1)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var matches = Entries.Where(predicate).ToArray();
            if (matches.Length >= expected)
            {
                return matches;
            }

            await Task.Delay(50);
        }

        return Entries.Where(predicate).ToArray();
    }

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

    private sealed class SinkLogger : ILogger
    {
        private readonly InMemoryLogSink sink;
        private readonly string category;

        public SinkLogger(InMemoryLogSink sink, string category)
        {
            this.sink = sink;
            this.category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => sink.scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scope = new Dictionary<string, object?>();
            sink.scopes.ForEachScope(
                (value, target) =>
                {
                    foreach (var pair in ToDictionary(value))
                    {
                        target[pair.Key] = pair.Value;
                    }
                },
                scope);
            sink.entries.Enqueue(new CapturedLog(category, logLevel, eventId, formatter(state, exception), ToDictionary(state), scope));
        }
    }
}
