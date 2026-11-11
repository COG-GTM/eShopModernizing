using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Persistence;
using eShop.Catalog.Data.Services;
using static eShop.Catalog.Data.Tests.S1_Data.TestSupport;

namespace eShop.Catalog.Data.Tests.S1_Data;

/// <summary>EF Core CatalogService behaviour on the in-memory provider (SQL Server coverage is in Integration/).</summary>
public class CatalogServiceTests
{
    private static async Task<(CatalogDbContext Db, CatalogService Service)> SeededAsync(string? name = null)
    {
        var db = InMemoryContext(name);
        await Seeder().SeedAsync(db);
        db.ChangeTracker.Clear();
        return (db, new CatalogService(db));
    }

    [Theory]
    [InlineData(10, 0, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    [InlineData(10, 1, 2, new[] { 11, 12 })]
    [InlineData(5, 2, 3, new[] { 11, 12 })]
    [InlineData(4, 1, 3, new[] { 5, 6, 7, 8 })]
    [InlineData(12, 0, 1, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
    [InlineData(7, 0, 2, new[] { 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData(10, 5, 2, new int[0])]
    public async Task Paging_maths_match_legacy(int pageSize, int pageIndex, int expectedPages, int[] expectedIds)
    {
        var (db, service) = await SeededAsync();
        await using var _ = db;

        var page = service.GetCatalogItemsPaginated(pageSize, pageIndex);

        Assert.Equal((pageIndex, pageSize, 12L, expectedPages), (page.ActualPage, page.ItemsPerPage, page.TotalItems, page.TotalPages));
        Assert.Equal(expectedIds, page.Data.Select(i => i.Id));
        Assert.All(page.Data, i => Assert.Equal(i.CatalogBrandId, i.CatalogBrand!.Id));
        Assert.All(page.Data, i => Assert.Equal(i.CatalogTypeId, i.CatalogType!.Id));
    }

    [Fact]
    public async Task Find_includes_brand_and_type_and_returns_null_when_missing()
    {
        var (db, service) = await SeededAsync();
        await using var _ = db;

        var item = service.FindCatalogItem(1)!;

        Assert.Equal((".NET", "T-Shirt"), (item.CatalogBrand!.Brand, item.CatalogType!.Type));
        Assert.Null(service.FindCatalogItem(999));
    }

    [Fact]
    public async Task Brands_and_types_are_listed_in_id_order()
    {
        var (db, service) = await SeededAsync();
        await using var _ = db;

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, service.GetCatalogBrands().Select(b => b.Id));
        Assert.Equal(new[] { 1, 2, 3, 4 }, service.GetCatalogTypes().Select(t => t.Id));
    }

    [Fact]
    public async Task Create_ignores_posted_id()
    {
        var (db, service) = await SeededAsync();
        await using var _ = db;
        var item = new CatalogItem { Id = 5, Name = "Lip Oil", CatalogBrandId = 5, CatalogTypeId = 1, Price = 42M };

        service.CreateCatalogItem(item);

        Assert.NotEqual(5, item.Id);
        Assert.Equal(13, service.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.Equal(".NET Black & White Mug", service.FindCatalogItem(2)!.Name);
    }

    [Fact]
    public async Task Update_with_detached_instance_works_even_when_original_is_tracked()
    {
        var (db, service) = await SeededAsync();
        await using var _ = db;
        var tracked = service.FindCatalogItem(3)!;

        service.UpdateCatalogItem(new CatalogItem
        {
            Id = 3, Name = "Prism White T-Shirt v2", Description = "d", Price = 13.25M, PictureFileName = "3.png",
            CatalogBrandId = 2, CatalogTypeId = 2, AvailableStock = 7,
        });

        Assert.Equal(("Prism White T-Shirt v2", 13.25M, 2, 7), (tracked.Name, tracked.Price, tracked.CatalogBrandId, tracked.AvailableStock));
        db.ChangeTracker.Clear();
        Assert.Equal("Prism White T-Shirt v2", service.FindCatalogItem(3)!.Name);
    }

    [Fact]
    public async Task Remove_deletes_found_item()
    {
        var (db, service) = await SeededAsync();
        await using var _ = db;

        service.RemoveCatalogItem(service.FindCatalogItem(12)!);

        Assert.Null(service.FindCatalogItem(12));
        Assert.Equal(11, service.GetCatalogItemsPaginated(10, 0).TotalItems);
    }
}
