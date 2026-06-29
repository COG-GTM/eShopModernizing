using eShopCoreModernized.Models;
using eShopCoreModernized.Services;

namespace eShopCoreModernized.Tests;

public class CatalogServiceMockTests
{
    private readonly CatalogServiceMock _service;

    public CatalogServiceMockTests()
    {
        _service = new CatalogServiceMock();
    }

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsCorrectPage()
    {
        var result = _service.GetCatalogItemsPaginated(2, 0);

        Assert.Equal(2, result.Data.Count());
        Assert.Equal(0, result.ActualPage);
        Assert.Equal(2, result.ItemsPerPage);
        Assert.Equal(5, result.TotalItems);
    }

    [Fact]
    public void GetCatalogItemsPaginated_SecondPage()
    {
        var result = _service.GetCatalogItemsPaginated(2, 1);

        Assert.Equal(2, result.Data.Count());
        Assert.Equal(1, result.ActualPage);
    }

    [Fact]
    public async Task GetCatalogItemsPaginatedAsync_DelegatesToSync()
    {
        var result = await _service.GetCatalogItemsPaginatedAsync(3, 0);

        Assert.Equal(3, result.Data.Count());
    }

    [Fact]
    public void FindCatalogItem_ReturnsItem_WhenExists()
    {
        var item = _service.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.Equal(1, item.Id);
        Assert.Equal(".NET Bot Black Hoodie", item.Name);
    }

    [Fact]
    public void FindCatalogItem_ReturnsNull_WhenNotFound()
    {
        var item = _service.FindCatalogItem(999);

        Assert.Null(item);
    }

    [Fact]
    public async Task FindCatalogItemAsync_ReturnsItem()
    {
        var item = await _service.FindCatalogItemAsync(2);

        Assert.NotNull(item);
        Assert.Equal(2, item.Id);
    }

    [Fact]
    public void GetCatalogTypes_ReturnsMockTypes()
    {
        var types = _service.GetCatalogTypes();

        Assert.Equal(4, types.Count());
        Assert.Contains(types, t => t.Type == "Mug");
        Assert.Contains(types, t => t.Type == "T-Shirt");
    }

    [Fact]
    public async Task GetCatalogTypesAsync_ReturnsMockTypes()
    {
        var types = await _service.GetCatalogTypesAsync();

        Assert.Equal(4, types.Count());
    }

    [Fact]
    public void GetCatalogBrands_ReturnsMockBrands()
    {
        var brands = _service.GetCatalogBrands();

        Assert.Equal(5, brands.Count());
        Assert.Contains(brands, b => b.Brand == "Azure");
        Assert.Contains(brands, b => b.Brand == ".NET");
    }

    [Fact]
    public async Task GetCatalogBrandsAsync_ReturnsMockBrands()
    {
        var brands = await _service.GetCatalogBrandsAsync();

        Assert.Equal(5, brands.Count());
    }

    [Fact]
    public void CreateCatalogItem_AddsItemWithNextId()
    {
        var newItem = new CatalogItem { Name = "New Item", Price = 10M };

        _service.CreateCatalogItem(newItem);

        Assert.Equal(6, newItem.Id);
        var found = _service.FindCatalogItem(6);
        Assert.NotNull(found);
        Assert.Equal("New Item", found.Name);
    }

    [Fact]
    public async Task CreateCatalogItemAsync_AddsItem()
    {
        var newItem = new CatalogItem { Name = "Async Item", Price = 5M };

        await _service.CreateCatalogItemAsync(newItem);

        var found = _service.FindCatalogItem(newItem.Id);
        Assert.NotNull(found);
    }

    [Fact]
    public void UpdateCatalogItem_ReplacesExistingItem()
    {
        var updated = new CatalogItem { Id = 1, Name = "Updated Name", Price = 99M };

        _service.UpdateCatalogItem(updated);

        var found = _service.FindCatalogItem(1);
        Assert.NotNull(found);
        Assert.Equal("Updated Name", found.Name);
        Assert.Equal(99M, found.Price);
    }

    [Fact]
    public void UpdateCatalogItem_NoOp_WhenItemNotFound()
    {
        var nonExistent = new CatalogItem { Id = 999, Name = "Ghost" };

        _service.UpdateCatalogItem(nonExistent);

        Assert.Null(_service.FindCatalogItem(999));
    }

    [Fact]
    public async Task UpdateCatalogItemAsync_ReplacesItem()
    {
        var updated = new CatalogItem { Id = 2, Name = "Async Updated", Price = 50M };

        await _service.UpdateCatalogItemAsync(updated);

        var found = _service.FindCatalogItem(2);
        Assert.NotNull(found);
        Assert.Equal("Async Updated", found.Name);
    }

    [Fact]
    public void RemoveCatalogItem_RemovesExistingItem()
    {
        var item = _service.FindCatalogItem(1)!;

        _service.RemoveCatalogItem(item);

        Assert.Null(_service.FindCatalogItem(1));
    }

    [Fact]
    public async Task RemoveCatalogItemAsync_RemovesItem()
    {
        var item = (await _service.FindCatalogItemAsync(3))!;

        await _service.RemoveCatalogItemAsync(item);

        Assert.Null(_service.FindCatalogItem(3));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var exception = Record.Exception(() => _service.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_AssignsBrandsAndTypesToItems()
    {
        var item = _service.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.NotNull(item.CatalogBrand);
        Assert.Equal(".NET", item.CatalogBrand.Brand);
        Assert.NotNull(item.CatalogType);
        Assert.Equal("T-Shirt", item.CatalogType.Type);
    }
}
