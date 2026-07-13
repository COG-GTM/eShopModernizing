using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.EntityFrameworkCore;

namespace eShopModernized.Tests;

/// <summary>
/// Tests for the EF Core-backed <see cref="CatalogService"/> using the
/// in-memory provider. Each test spins up an isolated in-memory database
/// (unique name) and, where persistence matters, uses a fresh
/// <see cref="CatalogDBContext"/> per logical operation so results reflect
/// what was actually written to the store rather than the change tracker.
///
/// Note: create paths (<see cref="CatalogService.CreateCatalogItem"/> and its
/// async twin) rely on <see cref="CatalogItemHiLoGenerator"/>, which issues a
/// raw <c>SELECT NEXT VALUE FOR catalog_hilo</c> against the DB connection. The
/// in-memory provider has no relational connection, so those paths cannot be
/// exercised end-to-end here; they are documented via
/// <see cref="CreateCatalogItem_WithInMemoryProvider_ThrowsBecauseHiLoNeedsSql"/>.
/// </summary>
public class CatalogServiceTests
{
    private static DbContextOptions<CatalogDBContext> NewOptions() =>
        new DbContextOptionsBuilder<CatalogDBContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

    private static void Seed(DbContextOptions<CatalogDBContext> options)
    {
        using var db = new CatalogDBContext(options);

        db.CatalogBrands.AddRange(
            new CatalogBrand { Id = 1, Brand = "Azure" },
            new CatalogBrand { Id = 2, Brand = ".NET" });

        db.CatalogTypes.AddRange(
            new CatalogType { Id = 1, Type = "Mug" },
            new CatalogType { Id = 2, Type = "T-Shirt" });

        for (int i = 1; i <= 5; i++)
        {
            db.CatalogItems.Add(new CatalogItem
            {
                Id = i,
                Name = $"Item {i}",
                Description = $"Description {i}",
                Price = i,
                PictureFileName = $"{i}.png",
                CatalogBrandId = (i % 2) + 1,
                CatalogTypeId = (i % 2) + 1,
                AvailableStock = 100
            });
        }

        db.SaveChanges();
    }

    private static CatalogService NewService(DbContextOptions<CatalogDBContext> options) =>
        new CatalogService(new CatalogDBContext(options), new CatalogItemHiLoGenerator());

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsRequestedPage()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var result = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 0);

        Assert.Equal(5, result.TotalItems);
        Assert.Equal(2, result.ItemsPerPage);
        Assert.Equal(0, result.ActualPage);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(new[] { 1, 2 }, result.Data.Select(i => i.Id));
    }

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsPartialLastPage()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var result = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 2);

        Assert.Single(result.Data);
        Assert.Equal(5, result.Data.Single().Id);
    }

    [Fact]
    public void GetCatalogItemsPaginated_PastLastPage_ReturnsEmptyButKeepsTotals()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var result = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 10);

        Assert.Empty(result.Data);
        Assert.Equal(5, result.TotalItems);
    }

    [Fact]
    public void GetCatalogItemsPaginated_IncludesBrandAndTypeNavigations()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var result = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 0);

        Assert.All(result.Data, item =>
        {
            Assert.NotNull(item.CatalogBrand);
            Assert.NotNull(item.CatalogType);
        });
    }

    [Fact]
    public async Task GetCatalogItemsPaginatedAsync_ReturnsRequestedPage()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var result = await service.GetCatalogItemsPaginatedAsync(pageSize: 3, pageIndex: 0);

        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.Data.Count());
        Assert.Equal(new[] { 1, 2, 3 }, result.Data.Select(i => i.Id));
    }

    [Fact]
    public void FindCatalogItem_ExistingId_ReturnsItemWithNavigations()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var item = service.FindCatalogItem(3);

        Assert.NotNull(item);
        Assert.Equal(3, item!.Id);
        Assert.Equal("Item 3", item.Name);
        Assert.NotNull(item.CatalogBrand);
        Assert.NotNull(item.CatalogType);
    }

    [Fact]
    public void FindCatalogItem_UnknownId_ReturnsNull()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        Assert.Null(service.FindCatalogItem(999));
    }

    [Fact]
    public async Task FindCatalogItemAsync_ExistingId_ReturnsItem()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var item = await service.FindCatalogItemAsync(1);

        Assert.NotNull(item);
        Assert.Equal(1, item!.Id);
    }

    [Fact]
    public void GetCatalogBrands_ReturnsAllSeededBrands()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var brands = service.GetCatalogBrands().ToList();

        Assert.Equal(2, brands.Count);
        Assert.Contains(brands, b => b.Brand == "Azure");
        Assert.Contains(brands, b => b.Brand == ".NET");
    }

    [Fact]
    public async Task GetCatalogBrandsAsync_ReturnsAllSeededBrands()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var brands = await service.GetCatalogBrandsAsync();

        Assert.Equal(2, brands.Count());
    }

    [Fact]
    public void GetCatalogTypes_ReturnsAllSeededTypes()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var types = service.GetCatalogTypes().ToList();

        Assert.Equal(2, types.Count);
        Assert.Contains(types, t => t.Type == "Mug");
        Assert.Contains(types, t => t.Type == "T-Shirt");
    }

    [Fact]
    public async Task GetCatalogTypesAsync_ReturnsAllSeededTypes()
    {
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var types = await service.GetCatalogTypesAsync();

        Assert.Equal(2, types.Count());
    }

    [Fact]
    public void UpdateCatalogItem_PersistsChangedFields()
    {
        var options = NewOptions();
        Seed(options);

        using (var service = NewService(options))
        {
            var edited = new CatalogItem
            {
                Id = 2,
                Name = "Renamed",
                Description = "Updated description",
                Price = 42.5M,
                PictureFileName = "2.png",
                CatalogBrandId = 1,
                CatalogTypeId = 1,
                AvailableStock = 7
            };

            service.UpdateCatalogItem(edited);
        }

        using var verify = new CatalogDBContext(options);
        var stored = verify.CatalogItems.Single(i => i.Id == 2);
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal(42.5M, stored.Price);
        Assert.Equal(7, stored.AvailableStock);
    }

    [Fact]
    public async Task UpdateCatalogItemAsync_PersistsChangedFields()
    {
        var options = NewOptions();
        Seed(options);

        using (var service = NewService(options))
        {
            var edited = new CatalogItem
            {
                Id = 4,
                Name = "Async Renamed",
                Price = 99M,
                PictureFileName = "4.png",
                CatalogBrandId = 1,
                CatalogTypeId = 1
            };

            await service.UpdateCatalogItemAsync(edited);
        }

        using var verify = new CatalogDBContext(options);
        Assert.Equal("Async Renamed", verify.CatalogItems.Single(i => i.Id == 4).Name);
    }

    [Fact]
    public void RemoveCatalogItem_DeletesItemFromStore()
    {
        var options = NewOptions();
        Seed(options);

        using (var service = NewService(options))
        {
            var toRemove = service.FindCatalogItem(1);
            Assert.NotNull(toRemove);
            service.RemoveCatalogItem(toRemove!);
        }

        using var verify = new CatalogDBContext(options);
        Assert.Null(verify.CatalogItems.FirstOrDefault(i => i.Id == 1));
        Assert.Equal(4, verify.CatalogItems.Count());
    }

    [Fact]
    public async Task RemoveCatalogItemAsync_DeletesItemFromStore()
    {
        var options = NewOptions();
        Seed(options);

        using (var service = NewService(options))
        {
            var toRemove = await service.FindCatalogItemAsync(5);
            Assert.NotNull(toRemove);
            await service.RemoveCatalogItemAsync(toRemove!);
        }

        using var verify = new CatalogDBContext(options);
        Assert.Null(verify.CatalogItems.FirstOrDefault(i => i.Id == 5));
    }

    [Fact]
    public void CreateCatalogItem_WithInMemoryProvider_ThrowsBecauseHiLoNeedsSql()
    {
        // Documents the boundary: CreateCatalogItem delegates id assignment to
        // CatalogItemHiLoGenerator, which runs a raw SQL sequence query. The
        // in-memory provider exposes no relational connection, so this path is
        // not unit-testable end-to-end without a real SQL Server.
        var options = NewOptions();
        Seed(options);
        using var service = NewService(options);

        var newItem = new CatalogItem
        {
            Name = "New",
            Price = 1M,
            PictureFileName = "new.png",
            CatalogBrandId = 1,
            CatalogTypeId = 1
        };

        Assert.ThrowsAny<Exception>(() => service.CreateCatalogItem(newItem));
    }
}
