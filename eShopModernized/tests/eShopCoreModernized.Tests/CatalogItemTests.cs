using eShopCoreModernized.Models;

namespace eShopCoreModernized.Tests;

public class CatalogItemTests
{
    [Fact]
    public void DefaultConstructor_SetsPictureFileName()
    {
        var item = new CatalogItem();

        Assert.Equal(CatalogItem.DefaultPictureName, item.PictureFileName);
    }

    [Fact]
    public void DefaultPictureName_IsDummyPng()
    {
        Assert.Equal("dummy.png", CatalogItem.DefaultPictureName);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var item = new CatalogItem
        {
            Id = 42,
            Name = "Test Item",
            Description = "A test description",
            Price = 19.99M,
            PictureFileName = "test.png",
            PictureUri = "http://example.com/test.png",
            TempImageName = "temp.png",
            CatalogTypeId = 1,
            CatalogBrandId = 2,
            AvailableStock = 100,
            RestockThreshold = 10,
            MaxStockThreshold = 500,
            OnReorder = true
        };

        Assert.Equal(42, item.Id);
        Assert.Equal("Test Item", item.Name);
        Assert.Equal("A test description", item.Description);
        Assert.Equal(19.99M, item.Price);
        Assert.Equal("test.png", item.PictureFileName);
        Assert.Equal("http://example.com/test.png", item.PictureUri);
        Assert.Equal("temp.png", item.TempImageName);
        Assert.Equal(1, item.CatalogTypeId);
        Assert.Equal(2, item.CatalogBrandId);
        Assert.Equal(100, item.AvailableStock);
        Assert.Equal(10, item.RestockThreshold);
        Assert.Equal(500, item.MaxStockThreshold);
        Assert.True(item.OnReorder);
    }

    [Fact]
    public void NavigationProperties_DefaultToNull()
    {
        var item = new CatalogItem();

        Assert.Null(item.CatalogType);
        Assert.Null(item.CatalogBrand);
        Assert.Null(item.Description);
        Assert.Null(item.PictureUri);
        Assert.Null(item.TempImageName);
    }
}
