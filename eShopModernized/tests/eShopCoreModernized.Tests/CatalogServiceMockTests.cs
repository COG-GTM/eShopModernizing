using Xunit;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.ViewModel;

namespace eShopCoreModernized.Tests;

public class CatalogServiceMockTests
{
    private static CatalogServiceMock CreateService() => new();

    [Fact]
    public void GetCatalogBrands_ReturnsSeededBrands()
    {
        var service = CreateService();

        var brands = service.GetCatalogBrands().ToList();

        Assert.Equal(5, brands.Count);
        Assert.Contains(brands, b => b.Brand == "Azure");
        Assert.Contains(brands, b => b.Brand == ".NET");
    }

    [Fact]
    public async Task GetCatalogBrandsAsync_ReturnsSeededBrands()
    {
        var service = CreateService();

        var brands = (await service.GetCatalogBrandsAsync()).ToList();

        Assert.Equal(5, brands.Count);
    }

    [Fact]
    public void GetCatalogTypes_ReturnsSeededTypes()
    {
        var service = CreateService();

        var types = service.GetCatalogTypes().ToList();

        Assert.Equal(4, types.Count);
        Assert.Contains(types, t => t.Type == "Mug");
        Assert.Contains(types, t => t.Type == "T-Shirt");
    }

    [Fact]
    public async Task GetCatalogTypesAsync_ReturnsSeededTypes()
    {
        var service = CreateService();

        var types = (await service.GetCatalogTypesAsync()).ToList();

        Assert.Equal(4, types.Count);
    }

    [Theory]
    [InlineData(2, 0, 2)]
    [InlineData(3, 0, 3)]
    [InlineData(10, 0, 5)]
    [InlineData(2, 2, 1)]
    public void GetCatalogItemsPaginated_ReturnsCorrectPage(int pageSize, int pageIndex, int expectedCount)
    {
        var service = CreateService();

        var page = service.GetCatalogItemsPaginated(pageSize, pageIndex);

        Assert.Equal(expectedCount, page.Data.Count());
        Assert.Equal(pageIndex, page.ActualPage);
        Assert.Equal(pageSize, page.ItemsPerPage);
        Assert.Equal(5, page.TotalItems);
    }

    [Fact]
    public async Task GetCatalogItemsPaginatedAsync_MatchesSyncResult()
    {
        var service = CreateService();

        var page = await service.GetCatalogItemsPaginatedAsync(2, 1);

        Assert.Equal(2, page.Data.Count());
        Assert.Equal(1, page.ActualPage);
        Assert.Equal(5, page.TotalItems);
    }

    [Fact]
    public void FindCatalogItem_ReturnsItem_WhenIdExists()
    {
        var service = CreateService();

        var item = service.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.Equal(1, item!.Id);
        Assert.Equal(".NET Bot Black Hoodie", item.Name);
    }

    [Fact]
    public void FindCatalogItem_ReturnsNull_WhenIdUnknown()
    {
        var service = CreateService();

        Assert.Null(service.FindCatalogItem(999));
    }

    [Fact]
    public async Task FindCatalogItemAsync_ReturnsItem_WhenIdExists()
    {
        var service = CreateService();

        var item = await service.FindCatalogItemAsync(2);

        Assert.NotNull(item);
        Assert.Equal(2, item!.Id);
    }

    [Fact]
    public async Task FindCatalogItemAsync_ReturnsNull_WhenIdUnknown()
    {
        var service = CreateService();

        Assert.Null(await service.FindCatalogItemAsync(-1));
    }

    [Fact]
    public void CreateCatalogItem_AssignsMaxPlusOneId_AndAddsItem()
    {
        var service = CreateService();
        var newItem = new CatalogItem { Name = "New Item" };

        service.CreateCatalogItem(newItem);

        Assert.Equal(6, newItem.Id);
        Assert.Equal(6, service.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.NotNull(service.FindCatalogItem(6));
    }

    [Fact]
    public async Task CreateCatalogItemAsync_AddsItem()
    {
        var service = CreateService();
        var newItem = new CatalogItem { Name = "Async Item" };

        await service.CreateCatalogItemAsync(newItem);

        Assert.Equal(6, newItem.Id);
        Assert.NotNull(await service.FindCatalogItemAsync(6));
    }

    [Fact]
    public void UpdateCatalogItem_ReplacesExistingItem()
    {
        var service = CreateService();
        var updated = new CatalogItem { Id = 1, Name = "Updated Name", Price = 99M };

        service.UpdateCatalogItem(updated);

        var item = service.FindCatalogItem(1);
        Assert.NotNull(item);
        Assert.Equal("Updated Name", item!.Name);
        Assert.Equal(99M, item.Price);
    }

    [Fact]
    public async Task UpdateCatalogItemAsync_ReplacesExistingItem()
    {
        var service = CreateService();
        var updated = new CatalogItem { Id = 2, Name = "Async Updated" };

        await service.UpdateCatalogItemAsync(updated);

        var item = await service.FindCatalogItemAsync(2);
        Assert.Equal("Async Updated", item!.Name);
    }

    [Fact]
    public void RemoveCatalogItem_RemovesById()
    {
        var service = CreateService();

        service.RemoveCatalogItem(new CatalogItem { Id = 1 });

        Assert.Null(service.FindCatalogItem(1));
        Assert.Equal(4, service.GetCatalogItemsPaginated(100, 0).TotalItems);
    }

    [Fact]
    public async Task RemoveCatalogItemAsync_RemovesById()
    {
        var service = CreateService();

        await service.RemoveCatalogItemAsync(new CatalogItem { Id = 3 });

        Assert.Null(await service.FindCatalogItemAsync(3));
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var service = CreateService();

        var exception = Record.Exception(() => service.Dispose());

        Assert.Null(exception);
    }
}
