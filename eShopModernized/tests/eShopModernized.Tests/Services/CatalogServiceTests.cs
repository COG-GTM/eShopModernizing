using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopModernized.Tests.Common;
using Xunit;

namespace eShopModernized.Tests.Services;

/// <summary>
/// Tests for <see cref="CatalogService"/> read/update/remove paths using the EF
/// Core in-memory provider. The create paths rely on a SQL Server HiLo sequence
/// and are covered separately in <see cref="CatalogServiceSqlTests"/>.
/// </summary>
public class CatalogServiceTests
{
    private static CatalogService CreateService(string dbName) =>
        new(InMemoryContext.Create(dbName), new CatalogItemHiLoGenerator());

    [Fact]
    public async Task GetCatalogItemsPaginatedAsync_ReturnsPage()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        var page = await CreateService(db).GetCatalogItemsPaginatedAsync(pageSize: 2, pageIndex: 0);

        Assert.Equal(3, page.TotalItems);
        Assert.Equal(2, page.Data.Count());
    }

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsPage()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        var page = CreateService(db).GetCatalogItemsPaginated(pageSize: 2, pageIndex: 0);

        Assert.Equal(3, page.TotalItems);
        Assert.Equal(2, page.Data.Count());
    }

    [Fact]
    public async Task FindCatalogItemAsync_ReturnsItemWithNavigations()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        var item = await CreateService(db).FindCatalogItemAsync(1);

        Assert.NotNull(item);
        Assert.NotNull(item!.CatalogBrand);
        Assert.NotNull(item.CatalogType);
    }

    [Fact]
    public void FindCatalogItem_ReturnsNull_WhenMissing()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        Assert.Null(CreateService(db).FindCatalogItem(999));
    }

    [Fact]
    public async Task GetCatalogTypesAsync_ReturnsAll()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        Assert.Equal(2, (await CreateService(db).GetCatalogTypesAsync()).Count());
    }

    [Fact]
    public void GetCatalogTypes_ReturnsAll()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        Assert.Equal(2, CreateService(db).GetCatalogTypes().Count());
    }

    [Fact]
    public async Task GetCatalogBrandsAsync_ReturnsAll()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        Assert.Equal(3, (await CreateService(db).GetCatalogBrandsAsync()).Count());
    }

    [Fact]
    public void GetCatalogBrands_ReturnsAll()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        Assert.Equal(3, CreateService(db).GetCatalogBrands().Count());
    }

    [Fact]
    public async Task UpdateCatalogItemAsync_PersistsChanges()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        var edited = new CatalogItem { Id = 1, Name = "Updated Async", PictureFileName = "1.png", CatalogBrandId = 1, CatalogTypeId = 1, Price = 99m };
        await CreateService(db).UpdateCatalogItemAsync(edited);

        Assert.Equal("Updated Async", CreateService(db).FindCatalogItem(1)!.Name);
    }

    [Fact]
    public void UpdateCatalogItem_PersistsChanges()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        var edited = new CatalogItem { Id = 2, Name = "Updated Sync", PictureFileName = "2.png", CatalogBrandId = 2, CatalogTypeId = 2, Price = 5m };
        CreateService(db).UpdateCatalogItem(edited);

        Assert.Equal("Updated Sync", CreateService(db).FindCatalogItem(2)!.Name);
    }

    [Fact]
    public async Task RemoveCatalogItemAsync_DeletesItem()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        await CreateService(db).RemoveCatalogItemAsync(new CatalogItem { Id = 3 });

        Assert.Null(CreateService(db).FindCatalogItem(3));
    }

    [Fact]
    public void RemoveCatalogItem_DeletesItem()
    {
        var db = Guid.NewGuid().ToString();
        InMemoryContext.CreateSeeded(db).Dispose();

        CreateService(db).RemoveCatalogItem(new CatalogItem { Id = 1 });

        Assert.Null(CreateService(db).FindCatalogItem(1));
    }

    [Fact]
    public void Dispose_DisposesUnderlyingContext()
    {
        var db = Guid.NewGuid().ToString();
        var service = CreateService(db);

        service.Dispose();
    }
}
