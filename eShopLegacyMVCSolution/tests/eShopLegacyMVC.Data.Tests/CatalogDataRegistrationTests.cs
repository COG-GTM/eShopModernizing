using System;
using System.Collections.Generic;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Infrastructure;
using eShopLegacyMVC.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace eShopLegacyMVC.Data.Tests
{
    public class CatalogDataRegistrationTests
    {
        private static ServiceProvider Build(Dictionary<string, string> settings, Action<CatalogDataOptions> configure = null)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new ServiceCollection().AddCatalogData(configuration, configure).BuildServiceProvider(validateScopes: true);
        }

        [Fact]
        public void Mock_mode_registers_a_singleton_mock_service()
        {
            using var provider = Build(new Dictionary<string, string> { ["UseMockData"] = "true" });

            ICatalogService first, second;
            using (var scope = provider.CreateScope()) first = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            using (var scope = provider.CreateScope()) second = scope.ServiceProvider.GetRequiredService<ICatalogService>();

            Assert.IsType<CatalogServiceMock>(first);
            Assert.Same(first, second);
        }

        [Fact]
        public void Database_mode_registers_scoped_service_context_and_singleton_hilo_generator()
        {
            using var provider = Build(new Dictionary<string, string>
            {
                ["UseMockData"] = "false",
                ["ConnectionStrings:CatalogDBContext"] = OfflineContext.UnreachableConnectionString,
            });

            using var scope1 = provider.CreateScope();
            using var scope2 = provider.CreateScope();
            var service1 = scope1.ServiceProvider.GetRequiredService<ICatalogService>();

            Assert.IsType<CatalogService>(service1);
            Assert.NotSame(service1, scope2.ServiceProvider.GetRequiredService<ICatalogService>());
            Assert.Same(scope1.ServiceProvider.GetRequiredService<CatalogDBContext>(), scope1.ServiceProvider.GetRequiredService<CatalogDBContext>());
            Assert.Same(scope1.ServiceProvider.GetRequiredService<CatalogItemHiLoGenerator>(), scope2.ServiceProvider.GetRequiredService<CatalogItemHiLoGenerator>());
            Assert.NotNull(scope1.ServiceProvider.GetRequiredService<CatalogDBInitializer>());
        }

        [Fact]
        public void Legacy_appSettings_keys_bind_and_can_be_overridden()
        {
            using var provider = Build(
                new Dictionary<string, string> { ["UseMockData"] = "true", ["UseCustomizationData"] = "true" },
                o => o.ContentRootPath = "/app");

            var options = provider.GetRequiredService<IOptions<CatalogDataOptions>>().Value;

            Assert.True(options.UseMockData);
            Assert.True(options.UseCustomizationData);
            Assert.Equal("/app", options.ContentRootPath);
        }

        [Fact]
        public void Missing_connection_string_fails_when_the_context_is_resolved()
        {
            using var provider = Build(new Dictionary<string, string>());
            using var scope = provider.CreateScope();

            var ex = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<CatalogDBContext>().Model);

            Assert.Contains("CatalogDBContext", ex.Message);
        }

        [Fact]
        public void InitializeCatalogDatabase_is_a_no_op_in_mock_mode()
        {
            using var provider = Build(new Dictionary<string, string> { ["UseMockData"] = "true" });

            provider.InitializeCatalogDatabase();
        }
    }
}
