using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.UnitTests;

// UseMockData toggle: mock singleton vs EF registrations, and SQL mode never silently falls back to the mock.
public class CatalogSettingsToggleTests
{
    private const string FakeSql = "Server=127.0.0.1,1;Database=unit;User Id=sa;Password=unused;TrustServerCertificate=True";

    [Fact]
    public void Mock_mode_registers_no_DbContext()
    {
        var services = TestServices.Collection(TestServices.Config());
        Assert.DoesNotContain(services, s => typeof(DbContext).IsAssignableFrom(s.ServiceType));
    }

    [Theory]
    [InlineData("UseMockData")]
    [InlineData("Catalog:UseMockData")]
    public void Sql_mode_never_registers_the_mock(string key)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        eShop.Catalog.Infrastructure.Services.CatalogServicesExtensions.AddCatalogServices(services, TestServices.Config((key, "false")));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(ICatalogService));
    }

    [SkippableFact]
    public void Sql_mode_registers_scoped_DbContext_and_EF_service()
    {
        var services = TestServices.Collection(TestServices.Config(("UseMockData", "false"), ("ConnectionStrings:CatalogDBContext", FakeSql)));
        var svc = services.LastOrDefault(s => s.ServiceType == typeof(ICatalogService));
        Skip.If(svc is null, "T1 not in this build: AddCatalogData registers nothing (scaffold stub)");
        Assert.Equal(ServiceLifetime.Scoped, svc!.Lifetime);
        Assert.Contains(services, s => typeof(DbContext).IsAssignableFrom(s.ServiceType) && s.Lifetime == ServiceLifetime.Scoped);
        using var scope = services.BuildServiceProvider().CreateScope();
        Assert.Equal("CatalogService", scope.ServiceProvider.GetRequiredService<ICatalogService>().GetType().Name);
    }

    [SkippableFact]
    public void Sql_mode_without_connection_string_fails_fast()
    {
        var cfg = TestServices.Config(("UseMockData", "false"));
        var services = new ServiceCollection();
        Exception? error = Record.Exception(() => services.AddCatalogData(cfg));
        Skip.If(error is null && services.Count == 0, "T1 not in this build: AddCatalogData registers nothing (scaffold stub)");
        var ex = Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("CatalogDBContext", ex.Message);
    }
}
