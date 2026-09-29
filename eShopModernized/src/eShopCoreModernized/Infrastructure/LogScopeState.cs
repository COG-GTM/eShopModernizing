using System.Collections;

namespace eShopCoreModernized.Infrastructure
{
    public sealed class LogScopeState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly List<KeyValuePair<string, object?>> _properties = new();

        public int Count => _properties.Count;

        public KeyValuePair<string, object?> this[int index] => _properties[index];

        public static LogScopeState ForDeployment(DeploymentInfo deployment) => new LogScopeState()
            .Add("ServiceName", deployment.ServiceName)
            .Add("ServiceVersion", deployment.Version)
            .Add("DeploymentTrack", deployment.Track);

        public LogScopeState Add(string key, object? value)
        {
            _properties.Add(new KeyValuePair<string, object?>(key, value));
            return this;
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _properties.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => string.Join(", ", _properties.Select(p => $"{p.Key}:{p.Value}"));
    }
}
