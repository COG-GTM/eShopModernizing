using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Seeding;
using eShop.Catalog.Data.Services;

namespace eShop.Catalog.Data.Tests.S0_Scaffold;

public class CatalogServiceMockTests
{
    private readonly CatalogServiceMock service = new();

    [Fact]
    public void Seed_matches_legacy_counts()
    {
        Assert.Equal(12, service.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.Equal(5, service.GetCatalogBrands().Count());
        Assert.Equal(4, service.GetCatalogTypes().Count());
    }

    [Fact]
    public void Brands_and_types_match_legacy_preconfigured_data()
    {
        Assert.Equal(
            new[] { (1, "Azure"), (2, ".NET"), (3, "Visual Studio"), (4, "SQL Server"), (5, "Other") },
            service.GetCatalogBrands().Select(b => (b.Id, b.Brand)));
        Assert.Equal(
            new[] { (1, "Mug"), (2, "T-Shirt"), (3, "Sheet"), (4, "USB Memory Stick") },
            service.GetCatalogTypes().Select(t => (t.Id, t.Type)));
    }

    [Fact]
    public void Items_match_legacy_preconfigured_data()
    {
        var items = service.GetCatalogItemsPaginated(12, 0).Data.ToList();

        Assert.Equal(Enumerable.Range(1, 12), items.Select(i => i.Id));
        var first = items[0];
        Assert.Equal(".NET Bot Black Hoodie", first.Name);
        Assert.Equal(".NET Bot Black Hoodie", first.Description);
        Assert.Equal(19.5M, first.Price);
        Assert.Equal("1.png", first.PictureFileName);
        Assert.Equal(2, first.CatalogTypeId);
        Assert.Equal(2, first.CatalogBrandId);
        Assert.Equal(100, first.AvailableStock);
        Assert.Equal("Cup<T> White Mug", items[8].Name);
        Assert.Equal(8.50M, items[1].Price);
        Assert.All(items, i => Assert.Equal(100, i.AvailableStock));
        Assert.All(items, i => Assert.Equal($"{i.Id}.png", i.PictureFileName));
    }

    [Fact]
    public void Paginated_items_have_brand_and_type_composed()
    {
        var items = service.GetCatalogItemsPaginated(12, 0).Data.ToList();

        Assert.All(items, i => Assert.Equal(i.CatalogBrandId, i.CatalogBrand!.Id));
        Assert.All(items, i => Assert.Equal(i.CatalogTypeId, i.CatalogType!.Id));
        Assert.Equal(".NET", items[0].CatalogBrand!.Brand);
        Assert.Equal("T-Shirt", items[0].CatalogType!.Type);
    }

    [Theory]
    [InlineData(10, 0, 10, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
    [InlineData(10, 1, 2, 2, new[] { 11, 12 })]
    [InlineData(5, 2, 2, 3, new[] { 11, 12 })]
    [InlineData(4, 1, 4, 3, new[] { 5, 6, 7, 8 })]
    [InlineData(12, 0, 12, 1, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
    [InlineData(10, 5, 0, 2, new int[0])]
    public void GetCatalogItemsPaginated_paging_maths(int pageSize, int pageIndex, int expectedOnPage, int expectedPages, int[] expectedIds)
    {
        var page = service.GetCatalogItemsPaginated(pageSize, pageIndex);

        Assert.Equal(pageIndex, page.ActualPage);
        Assert.Equal(pageSize, page.ItemsPerPage);
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(expectedPages, page.TotalPages);
        Assert.Equal(expectedOnPage, page.Data.Count());
        Assert.Equal(expectedIds, page.Data.Select(i => i.Id));
    }

    [Fact]
    public void Create_assigns_next_id_and_update_remove_mutate_in_memory()
    {
        var item = new CatalogItem { Name = "MECCA Lip Oil", CatalogBrandId = 5, CatalogTypeId = 1, Price = 42M };

        service.CreateCatalogItem(item);
        Assert.Equal(13, item.Id);
        Assert.Equal(CatalogItem.DefaultPictureName, item.PictureFileName);
        Assert.Same(item, service.FindCatalogItem(13));

        var replacement = new CatalogItem { Id = 13, Name = "MECCA Lip Oil v2", CatalogBrandId = 5, CatalogTypeId = 1 };
        service.UpdateCatalogItem(replacement);
        Assert.Equal("MECCA Lip Oil v2", service.FindCatalogItem(13)!.Name);

        service.RemoveCatalogItem(replacement);
        Assert.Null(service.FindCatalogItem(13));
        Assert.Equal(12, service.GetCatalogItemsPaginated(10, 0).TotalItems);
    }

    [Fact]
    public void Seed_returns_fresh_instances_per_call()
    {
        Assert.NotSame(PreconfiguredData.GetPreconfiguredCatalogItems()[0], PreconfiguredData.GetPreconfiguredCatalogItems()[0]);
    }
}
