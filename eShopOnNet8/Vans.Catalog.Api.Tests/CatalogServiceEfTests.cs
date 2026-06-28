using Microsoft.EntityFrameworkCore;
using Vans.Catalog.Api.Models;
using Vans.Catalog.Api.Services;
using Xunit;

namespace Vans.Catalog.Api.Tests;

public class CatalogServiceEfTests
{
    private static CatalogService CreateService()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new CatalogDbContext(options);
        db.Database.EnsureCreated();
        return new CatalogService(db);
    }

    [Fact]
    public void Seed_Has_Expected_Counts()
    {
        var service = CreateService();

        Assert.Equal(12, service.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.Equal(5, service.GetCatalogBrands().Count());
        Assert.Equal(4, service.GetCatalogTypes().Count());
    }

    [Fact]
    public void FindCatalogItem_Includes_Brand_And_Type()
    {
        var service = CreateService();

        var item = service.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.Equal(".NET", item!.CatalogBrand!.Brand);
        Assert.Equal("T-Shirt", item.CatalogType!.Type);
    }

    [Fact]
    public void Create_Then_Delete_RoundTrips()
    {
        var service = CreateService();

        var created = service.CreateCatalogItem(new CatalogItem { Name = "Vans Sk8-Hi", Price = 75M, CatalogBrandId = 5, CatalogTypeId = 2 });
        Assert.True(created.Id > 12);
        Assert.Equal(13, service.GetCatalogItemsPaginated(100, 0).TotalItems);

        service.RemoveCatalogItem(created);
        Assert.Null(service.FindCatalogItem(created.Id));
        Assert.Equal(12, service.GetCatalogItemsPaginated(100, 0).TotalItems);
    }
}
