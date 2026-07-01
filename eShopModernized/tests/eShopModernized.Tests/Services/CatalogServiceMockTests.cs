using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Xunit;

namespace eShopModernized.Tests.Services;

public class CatalogServiceMockTests
{
    private readonly CatalogServiceMock _service = new();

    [Fact]
    public void GetCatalogBrands_ReturnsSeededBrands()
    {
        Assert.Equal(5, _service.GetCatalogBrands().Count());
    }

    [Fact]
    public async Task GetCatalogBrandsAsync_ReturnsSeededBrands()
    {
        Assert.Equal(5, (await _service.GetCatalogBrandsAsync()).Count());
    }

    [Fact]
    public void GetCatalogTypes_ReturnsSeededTypes()
    {
        Assert.Equal(4, _service.GetCatalogTypes().Count());
    }

    [Fact]
    public async Task GetCatalogTypesAsync_ReturnsSeededTypes()
    {
        Assert.Equal(4, (await _service.GetCatalogTypesAsync()).Count());
    }

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsRequestedPage()
    {
        var page = _service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 1);

        Assert.Equal(5, page.TotalItems);
        Assert.Equal(2, page.Data.Count());
        Assert.Equal(1, page.ActualPage);
    }

    [Fact]
    public async Task GetCatalogItemsPaginatedAsync_ReturnsRequestedPage()
    {
        var page = await _service.GetCatalogItemsPaginatedAsync(pageSize: 10, pageIndex: 0);

        Assert.Equal(5, page.Data.Count());
    }

    [Fact]
    public void FindCatalogItem_ReturnsItem_WhenExists()
    {
        Assert.NotNull(_service.FindCatalogItem(1));
    }

    [Fact]
    public async Task FindCatalogItemAsync_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _service.FindCatalogItemAsync(999));
    }

    [Fact]
    public void CreateCatalogItem_AddsItemWithNewId()
    {
        var item = new CatalogItem { Name = "New", CatalogBrandId = 1, CatalogTypeId = 1 };

        _service.CreateCatalogItem(item);

        Assert.Equal(6, item.Id);
        Assert.NotNull(_service.FindCatalogItem(6));
    }

    [Fact]
    public async Task CreateCatalogItemAsync_AddsItem()
    {
        var item = new CatalogItem { Name = "Async", CatalogBrandId = 1, CatalogTypeId = 1 };

        await _service.CreateCatalogItemAsync(item);

        Assert.NotNull(await _service.FindCatalogItemAsync(item.Id));
    }

    [Fact]
    public void UpdateCatalogItem_ReplacesExistingItem()
    {
        var updated = new CatalogItem { Id = 1, Name = "Renamed", CatalogBrandId = 1, CatalogTypeId = 1 };

        _service.UpdateCatalogItem(updated);

        Assert.Equal("Renamed", _service.FindCatalogItem(1)!.Name);
    }

    [Fact]
    public void UpdateCatalogItem_DoesNothing_WhenItemMissing()
    {
        var countBefore = _service.GetCatalogItemsPaginated(100, 0).Data.Count();

        _service.UpdateCatalogItem(new CatalogItem { Id = 999, Name = "Ghost" });

        Assert.Equal(countBefore, _service.GetCatalogItemsPaginated(100, 0).Data.Count());
    }

    [Fact]
    public async Task UpdateCatalogItemAsync_ReplacesExistingItem()
    {
        var updated = new CatalogItem { Id = 2, Name = "AsyncRenamed", CatalogBrandId = 1, CatalogTypeId = 1 };

        await _service.UpdateCatalogItemAsync(updated);

        Assert.Equal("AsyncRenamed", (await _service.FindCatalogItemAsync(2))!.Name);
    }

    [Fact]
    public void RemoveCatalogItem_RemovesItem()
    {
        _service.RemoveCatalogItem(new CatalogItem { Id = 3 });

        Assert.Null(_service.FindCatalogItem(3));
    }

    [Fact]
    public async Task RemoveCatalogItemAsync_RemovesItem()
    {
        await _service.RemoveCatalogItemAsync(new CatalogItem { Id = 4 });

        Assert.Null(await _service.FindCatalogItemAsync(4));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        _service.Dispose();
    }
}
