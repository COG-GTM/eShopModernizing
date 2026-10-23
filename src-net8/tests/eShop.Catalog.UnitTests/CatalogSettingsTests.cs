using eShop.Catalog.Core;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure;
using eShop.Catalog.Infrastructure.DependencyInjection;
using eShop.Catalog.Infrastructure.Mock;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eShop.Catalog.UnitTests
{
    public class CatalogSettingsTests
    {
        private static IConfiguration Config(params (string Key, string Value)[] values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string>(v.Key, v.Value)))
                .Build();
        }

        [Fact]
        public void Defaults_are_mock_data_without_customization()
        {
            var settings = Config().GetCatalogSettings();

            Assert.True(settings.UseMockData);
            Assert.False(settings.UseCustomizationData);
            Assert.True(new CatalogSettings().UseMockData);
        }

        [Fact]
        public void Keys_and_connection_string_name_match_the_legacy_web_config()
        {
            Assert.Equal("UseMockData", CatalogSettings.UseMockDataKey);
            Assert.Equal("UseCustomizationData", CatalogSettings.UseCustomizationDataKey);
            Assert.Equal("CatalogDBContext", CatalogSettings.ConnectionStringName);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("true", true)]
        public void Explicit_values_are_parsed(string value, bool expected)
        {
            Assert.Equal(expected, Config(("UseMockData", value)).GetCatalogSettings().UseMockData);
        }

        [Fact]
        public void Environment_variables_override_appsettings()
        {
            var name = "ESHOP_T7_" + Guid.NewGuid().ToString("N") + "_";
            Environment.SetEnvironmentVariable(name + "UseMockData", "false");
            Environment.SetEnvironmentVariable(name + "UseCustomizationData", "true");
            try
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string> { ["UseMockData"] = "true", ["UseCustomizationData"] = "false" })
                    .AddEnvironmentVariables(name)
                    .Build();

                var settings = config.GetCatalogSettings();

                Assert.False(settings.UseMockData);
                Assert.True(settings.UseCustomizationData);
            }
            finally
            {
                Environment.SetEnvironmentVariable(name + "UseMockData", null);
                Environment.SetEnvironmentVariable(name + "UseCustomizationData", null);
            }
        }

        [Fact]
        public void Mock_mode_registers_a_singleton_CatalogServiceMock()
        {
            var services = new ServiceCollection().AddCatalogServices(Config()).BuildServiceProvider();

            var first = services.GetRequiredService<ICatalogService>();

            Assert.IsType<CatalogServiceMock>(first);
            Assert.Same(first, services.GetRequiredService<ICatalogService>());
        }

        [Fact]
        public void Sql_mode_registers_no_mock_service()
        {
            var services = new ServiceCollection().AddCatalogServices(Config(("UseMockData", "false")));

            Assert.DoesNotContain(services, d => d.ImplementationType == typeof(CatalogServiceMock));
        }
    }
}
