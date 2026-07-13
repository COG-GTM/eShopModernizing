using eShopCoreModernized.Models;
using eShopCoreModernized.Services;

namespace eShopModernized.Tests;

public class CatalogServiceMockTests
{
    [Fact]
    public void GetCatalogItemsPaginated_ReturnsRequestedPage()
    {
        var service = new CatalogServiceMock();

        var result = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 0);

        Assert.Equal(5, result.TotalItems);
        Assert.Equal(2, result.Data.Count());
        Assert.Equal(0, result.ActualPage);
        Assert.Equal(2, result.ItemsPerPage);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void FindCatalogItem_ReturnsMatchingItem()
    {
        var service = new CatalogServiceMock();

        var item = service.FindCatalogItem(2);

        Assert.NotNull(item);
        Assert.Equal(2, item!.Id);
        Assert.Equal(".NET Black & White Mug", item.Name);
    }

    [Fact]
    public void CreateCatalogItem_AssignsNewIdAndAddsItem()
    {
        var service = new CatalogServiceMock();
        var newItem = new CatalogItem { Name = "Test Item", Description = "Test", Price = 1M };

        service.CreateCatalogItem(newItem);

        Assert.Equal(6, newItem.Id);
        Assert.NotNull(service.FindCatalogItem(6));
    }

    [Fact]
    public void RemoveCatalogItem_DeletesItem()
    {
        var service = new CatalogServiceMock();
        var item = service.FindCatalogItem(1);
        Assert.NotNull(item);

        service.RemoveCatalogItem(item!);

        Assert.Null(service.FindCatalogItem(1));
    }
}
