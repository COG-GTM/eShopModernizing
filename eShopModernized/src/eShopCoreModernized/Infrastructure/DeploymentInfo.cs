using System.Reflection;

namespace eShopCoreModernized.Infrastructure
{
    public sealed class DeploymentInfo
    {
        public const string ConfigurationSection = "Deployment";
        public const string DefaultServiceName = "eshop-core-modernized";
        public const string DefaultTrack = "stable";

        public string ServiceName { get; init; } = DefaultServiceName;

        public string Version { get; init; } = DefaultVersion();

        public string Track { get; init; } = DefaultTrack;

        public string Instance { get; init; } = Environment.MachineName;

        public static DeploymentInfo FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection(ConfigurationSection);
            return new DeploymentInfo
            {
                ServiceName = ValueOrDefault(section["ServiceName"], DefaultServiceName),
                Version = ValueOrDefault(section["Version"], DefaultVersion()),
                Track = ValueOrDefault(section["Track"], DefaultTrack),
            };
        }

        private static string ValueOrDefault(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string DefaultVersion()
        {
            var assembly = typeof(DeploymentInfo).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
        }
    }
}
