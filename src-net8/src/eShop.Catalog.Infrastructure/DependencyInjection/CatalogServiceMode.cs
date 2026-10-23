using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Infrastructure.DependencyInjection
{
    /// <summary>The catalog backend selected by UseMockData at registration time ("mock" or "sql") and how it is registered.</summary>
    public sealed class CatalogServiceMode
    {
        public const string Mock = "mock";
        public const string Sql = "sql";

        public CatalogServiceMode(bool useMockData, ServiceLifetime lifetime, string implementation)
        {
            UseMockData = useMockData;
            Lifetime = lifetime;
            Implementation = implementation;
        }

        public bool UseMockData { get; }

        public string Name => UseMockData ? Mock : Sql;

        public ServiceLifetime Lifetime { get; }

        public string Implementation { get; }

        /// <summary>Legacy Autofac lifetimes: mock SingleInstance -> Singleton, SQL InstancePerLifetimeScope -> Scoped.</summary>
        public ServiceLifetime ExpectedLifetime => UseMockData ? ServiceLifetime.Singleton : ServiceLifetime.Scoped;
    }
}
