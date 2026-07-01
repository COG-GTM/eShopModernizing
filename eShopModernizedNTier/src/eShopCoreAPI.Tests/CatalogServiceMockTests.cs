using eShopCoreAPI.Models;
using eShopCoreAPI.Services;
using Xunit;

namespace eShopCoreAPI.Tests;

public class CatalogServiceMockTests
{
    private readonly CatalogServiceMock _service = new();

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsRequestedPage()
    {
        var page = _service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 1);

        Assert.Equal(5, page.Data.Count());
        Assert.Equal(12, page.Count);
        Assert.Equal(1, page.PageIndex);
        Assert.Equal(5, page.PageSize);
    }

    [Fact]
    public void FindCatalogItem_ReturnsItemWithBrandAndType()
    {
        var item = _service.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.NotNull(item!.CatalogBrand);
        Assert.NotNull(item.CatalogType);
    }

    [Fact]
    public void FindCatalogItem_UnknownId_ReturnsNull()
    {
        Assert.Null(_service.FindCatalogItem(9999));
    }

    [Fact]
    public void CreateCatalogItem_AssignsNewIdAndResolvesNavigation()
    {
        var newItem = new CatalogItem { Name = "New Mug", CatalogBrandId = 1, CatalogTypeId = 1, Price = 5M };

        _service.CreateCatalogItem(newItem);

        Assert.Equal(13, newItem.Id);
        Assert.NotNull(newItem.CatalogBrand);
        Assert.NotNull(newItem.CatalogType);
        Assert.NotNull(_service.FindCatalogItem(13));
    }

    [Fact]
    public void UpdateCatalogItem_ReplacesExistingItem()
    {
        var updated = new CatalogItem { Id = 2, Name = "Renamed", CatalogBrandId = 2, CatalogTypeId = 1, Price = 9M };

        _service.UpdateCatalogItem(updated);

        Assert.Equal("Renamed", _service.FindCatalogItem(2)!.Name);
    }

    [Fact]
    public void RemoveCatalogItem_RemovesExistingItem()
    {
        var item = _service.FindCatalogItem(3)!;

        _service.RemoveCatalogItem(item);

        Assert.Null(_service.FindCatalogItem(3));
    }

    [Fact]
    public void GetCatalogBrandsAndTypes_ReturnSeedData()
    {
        Assert.Equal(5, _service.GetCatalogBrands().Count());
        Assert.Equal(4, _service.GetCatalogTypes().Count());
    }
}
