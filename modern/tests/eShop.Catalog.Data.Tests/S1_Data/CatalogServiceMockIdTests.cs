using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;

namespace eShop.Catalog.Data.Tests.S1_Data;

public class CatalogServiceMockIdTests
{
    private static CatalogItem NewItem() => new() { Name = "n", CatalogBrandId = 1, CatalogTypeId = 1 };

    [Fact]
    public void Ids_are_max_plus_one_and_increase_per_create()
    {
        var mock = new CatalogServiceMock();
        var a = NewItem();
        var b = NewItem();

        mock.CreateCatalogItem(a);
        mock.CreateCatalogItem(b);

        Assert.Equal((13, 14), (a.Id, b.Id));
    }

    [Fact]
    public void Removing_a_middle_item_does_not_reuse_its_id()
    {
        var mock = new CatalogServiceMock();
        mock.RemoveCatalogItem(mock.FindCatalogItem(5)!);
        var item = NewItem();

        mock.CreateCatalogItem(item);

        Assert.Equal(13, item.Id);
    }

    [Fact]
    public void Removing_the_max_item_reuses_max_plus_one_like_legacy()
    {
        var mock = new CatalogServiceMock();
        mock.RemoveCatalogItem(mock.FindCatalogItem(12)!);
        var item = NewItem();

        mock.CreateCatalogItem(item);

        Assert.Equal(12, item.Id);
    }

    [Fact]
    public void Posted_id_is_overwritten()
    {
        var mock = new CatalogServiceMock();
        var item = NewItem();
        item.Id = 1;

        mock.CreateCatalogItem(item);

        Assert.Equal(13, item.Id);
        Assert.Equal(".NET Bot Black Hoodie", mock.FindCatalogItem(1)!.Name);
    }
}
