using Timberland.Catalog.Api.Models;
using Timberland.Catalog.Api.Services;
using Xunit;

namespace Timberland.Catalog.Api.Tests;

public class CatalogServiceMockTests
{
    private static CatalogServiceMock CreateService() => new();

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsFirstPage_WithCorrectTotals()
    {
        var service = CreateService();

        var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 0);

        Assert.Equal(0, page.ActualPage);
        Assert.Equal(5, page.ItemsPerPage);
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5, page.Data.Count());
        // Brand/Type navigation properties should be composed.
        Assert.All(page.Data, i =>
        {
            Assert.NotNull(i.CatalogBrand);
            Assert.NotNull(i.CatalogType);
        });
    }

    [Fact]
    public void GetCatalogItemsPaginated_SecondPage_ReturnsRemainingItems()
    {
        var service = CreateService();

        var page = service.GetCatalogItemsPaginated(pageSize: 10, pageIndex: 1);

        Assert.Equal(2, page.Data.Count());
        Assert.Equal(12, page.TotalItems);
    }

    [Fact]
    public void FindCatalogItem_ReturnsExpectedItem()
    {
        var service = CreateService();

        var item = service.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.Equal(".NET Bot Black Hoodie", item!.Name);
        Assert.Equal(19.5M, item.Price);
    }

    [Fact]
    public void FindCatalogItem_ReturnsNull_WhenMissing()
    {
        var service = CreateService();

        Assert.Null(service.FindCatalogItem(9999));
    }

    [Fact]
    public void CreateCatalogItem_AssignsNextId_AndIncreasesCount()
    {
        var service = CreateService();

        var newItem = new CatalogItem { Name = "Timberland Boot", CatalogBrandId = 1, CatalogTypeId = 1, Price = 199.99M };
        service.CreateCatalogItem(newItem);

        Assert.Equal(13, newItem.Id);
        Assert.Equal(13, service.GetCatalogItemsPaginated(50, 0).TotalItems);
        Assert.NotNull(service.FindCatalogItem(13));
    }

    [Fact]
    public void RemoveCatalogItem_RemovesItem()
    {
        var service = CreateService();
        var item = service.FindCatalogItem(2);
        Assert.NotNull(item);

        service.RemoveCatalogItem(item!);

        Assert.Null(service.FindCatalogItem(2));
        Assert.Equal(11, service.GetCatalogItemsPaginated(50, 0).TotalItems);
    }

    [Fact]
    public void SeedData_HasExpectedBrandsAndTypes()
    {
        var service = CreateService();

        Assert.Equal(5, service.GetCatalogBrands().Count());
        Assert.Equal(4, service.GetCatalogTypes().Count());
    }
}
