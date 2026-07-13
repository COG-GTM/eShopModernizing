using eShopCoreModernized.Models;

namespace eShopModernized.Tests;

public class CatalogItemTests
{
    [Fact]
    public void Constructor_DefaultsPictureFileNameToDefaultPictureName()
    {
        var item = new CatalogItem();

        Assert.Equal(CatalogItem.DefaultPictureName, item.PictureFileName);
        Assert.Equal("dummy.png", CatalogItem.DefaultPictureName);
    }

    [Fact]
    public void Constructor_InitializesNameToEmptyStringAndReferencesToNull()
    {
        var item = new CatalogItem();

        Assert.Equal(string.Empty, item.Name);
        Assert.Null(item.Description);
        Assert.Null(item.PictureUri);
        Assert.Null(item.TempImageName);
        Assert.Null(item.CatalogBrand);
        Assert.Null(item.CatalogType);
        Assert.False(item.OnReorder);
    }

    [Fact]
    public void NavigationProperties_CanBeAssignedAndLinkToForeignKeys()
    {
        var brand = new CatalogBrand { Id = 3, Brand = "Contoso" };
        var type = new CatalogType { Id = 7, Type = "Mug" };

        var item = new CatalogItem
        {
            Id = 1,
            Name = "Mug",
            Price = 9.99M,
            CatalogBrandId = brand.Id,
            CatalogBrand = brand,
            CatalogTypeId = type.Id,
            CatalogType = type,
        };

        Assert.Same(brand, item.CatalogBrand);
        Assert.Equal(item.CatalogBrandId, item.CatalogBrand!.Id);
        Assert.Same(type, item.CatalogType);
        Assert.Equal(item.CatalogTypeId, item.CatalogType!.Id);
    }

    [Fact]
    public void Properties_RoundTripAssignedValues()
    {
        var item = new CatalogItem
        {
            Id = 42,
            Name = "Widget",
            Description = "A useful widget",
            Price = 123.45M,
            PictureFileName = "widget.png",
            PictureUri = "http://example/widget.png",
            TempImageName = "temp.png",
            AvailableStock = 10,
            RestockThreshold = 2,
            MaxStockThreshold = 20,
            OnReorder = true,
        };

        Assert.Equal(42, item.Id);
        Assert.Equal("Widget", item.Name);
        Assert.Equal("A useful widget", item.Description);
        Assert.Equal(123.45M, item.Price);
        Assert.Equal("widget.png", item.PictureFileName);
        Assert.Equal("http://example/widget.png", item.PictureUri);
        Assert.Equal("temp.png", item.TempImageName);
        Assert.Equal(10, item.AvailableStock);
        Assert.Equal(2, item.RestockThreshold);
        Assert.Equal(20, item.MaxStockThreshold);
        Assert.True(item.OnReorder);
    }
}
