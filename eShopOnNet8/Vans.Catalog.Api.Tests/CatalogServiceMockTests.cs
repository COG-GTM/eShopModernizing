using Vans.Catalog.Api.Models;
using Vans.Catalog.Api.Services;
using Xunit;

namespace Vans.Catalog.Api.Tests;

public class CatalogServiceMockTests
{
    private static CatalogServiceMock CreateService() => new();

    [Fact]
    public void Seed_Has_Expected_Counts()
    {
        var service = CreateService();

        Assert.Equal(12, service.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.Equal(5, service.GetCatalogBrands().Count());
        Assert.Equal(4, service.GetCatalogTypes().Count());
    }

    [Fact]
    public void GetCatalogItemsPaginated_Returns_Correct_Page()
    {
        var service = CreateService();

        var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 1);

        Assert.Equal(5, page.ItemsPerPage);
        Assert.Equal(1, page.ActualPage);
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5, page.Data.Count());
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, page.Data.Select(i => i.Id).ToArray());
        Assert.All(page.Data, i => Assert.NotNull(i.CatalogBrand));
        Assert.All(page.Data, i => Assert.NotNull(i.CatalogType));
    }

    [Fact]
    public void FindCatalogItem_Returns_Item_When_Present_And_Null_Otherwise()
    {
        var service = CreateService();

        var found = service.FindCatalogItem(1);
        Assert.NotNull(found);
        Assert.Equal(".NET Bot Black Hoodie", found!.Name);
        Assert.Equal(19.5M, found.Price);

        Assert.Null(service.FindCatalogItem(9999));
    }

    [Fact]
    public void CreateCatalogItem_Assigns_New_Id_And_Persists()
    {
        var service = CreateService();

        var created = service.CreateCatalogItem(new CatalogItem { Name = "Vans Old Skool", Price = 65M, CatalogBrandId = 5, CatalogTypeId = 2 });

        Assert.Equal(13, created.Id);
        Assert.Equal(13, service.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.NotNull(service.FindCatalogItem(13));
    }

    [Fact]
    public void RemoveCatalogItem_Removes_Item()
    {
        var service = CreateService();

        var item = service.FindCatalogItem(3);
        Assert.NotNull(item);

        service.RemoveCatalogItem(item!);

        Assert.Null(service.FindCatalogItem(3));
        Assert.Equal(11, service.GetCatalogItemsPaginated(100, 0).TotalItems);
    }
}
