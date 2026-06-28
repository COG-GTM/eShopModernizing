using Catalog.Api.Models;
using Catalog.Api.Services;

namespace Catalog.Api.Tests;

public class CatalogServiceMockTests
{
    private static CatalogServiceMock NewService() => new();

    [Fact]
    public void GetCatalogItemsPaginated_ReturnsFirstPage_WithComposedBrandAndType()
    {
        var service = NewService();

        var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 0);

        Assert.Equal(12, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5, page.Data.Count());
        // ComposeCatalogItems must hydrate the navigation properties.
        Assert.All(page.Data, i =>
        {
            Assert.NotNull(i.CatalogBrand);
            Assert.NotNull(i.CatalogType);
        });
    }

    [Fact]
    public void GetCatalogItemsPaginated_SecondPage_SkipsCorrectly()
    {
        var service = NewService();

        var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 1);

        Assert.Equal(6, page.Data.First().Id);
        Assert.Equal(5, page.Data.Count());
    }

    [Fact]
    public void FindCatalogItem_ReturnsExpectedItem()
    {
        var service = NewService();

        var item = service.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.Equal(".NET Bot Black Hoodie", item!.Name);
    }

    [Fact]
    public void CreateCatalogItem_AssignsIncrementingId()
    {
        var service = NewService();

        var created = service.CreateCatalogItem(new CatalogItem { Name = "TNF Hoodie", CatalogBrandId = 2, CatalogTypeId = 2 });

        Assert.Equal(13, created.Id);
        Assert.NotNull(service.FindCatalogItem(13));
    }

    [Fact]
    public void RemoveCatalogItem_RemovesById()
    {
        var service = NewService();

        service.RemoveCatalogItem(new CatalogItem { Id = 1 });

        Assert.Null(service.FindCatalogItem(1));
    }

    [Fact]
    public void Seed_Contains_AllBrandsAndTypes()
    {
        var service = NewService();

        Assert.Equal(5, service.GetCatalogBrands().Count());
        Assert.Equal(4, service.GetCatalogTypes().Count());
    }
}
