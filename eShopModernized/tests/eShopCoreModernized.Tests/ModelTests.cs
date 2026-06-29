using Xunit;
using eShopCoreModernized.Models;

namespace eShopCoreModernized.Tests;

public class ModelTests
{
    [Fact]
    public void CatalogItem_DefaultsPictureFileNameToDummy()
    {
        var item = new CatalogItem();

        Assert.Equal(CatalogItem.DefaultPictureName, item.PictureFileName);
        Assert.Equal("dummy.png", CatalogItem.DefaultPictureName);
    }

    [Fact]
    public void CatalogItem_PropertiesRoundTrip()
    {
        var brand = new CatalogBrand { Id = 1, Brand = "Azure" };
        var type = new CatalogType { Id = 2, Type = "Mug" };

        var item = new CatalogItem
        {
            Id = 10,
            Name = "Test Item",
            Description = "A description",
            Price = 12.34M,
            PictureFileName = "10.png",
            PictureUri = "/pics/10/10.png",
            TempImageName = "temp.png",
            CatalogTypeId = 2,
            CatalogType = type,
            CatalogBrandId = 1,
            CatalogBrand = brand,
            AvailableStock = 100,
            RestockThreshold = 5,
            MaxStockThreshold = 200,
            OnReorder = true,
        };

        Assert.Equal(10, item.Id);
        Assert.Equal("Test Item", item.Name);
        Assert.Equal("A description", item.Description);
        Assert.Equal(12.34M, item.Price);
        Assert.Equal("10.png", item.PictureFileName);
        Assert.Equal("/pics/10/10.png", item.PictureUri);
        Assert.Equal("temp.png", item.TempImageName);
        Assert.Equal(2, item.CatalogTypeId);
        Assert.Same(type, item.CatalogType);
        Assert.Equal(1, item.CatalogBrandId);
        Assert.Same(brand, item.CatalogBrand);
        Assert.Equal(100, item.AvailableStock);
        Assert.Equal(5, item.RestockThreshold);
        Assert.Equal(200, item.MaxStockThreshold);
        Assert.True(item.OnReorder);
    }

    [Fact]
    public void CatalogBrand_PropertiesRoundTrip()
    {
        var brand = new CatalogBrand { Id = 3, Brand = "Visual Studio" };

        Assert.Equal(3, brand.Id);
        Assert.Equal("Visual Studio", brand.Brand);
    }

    [Fact]
    public void CatalogBrand_BrandDefaultsToEmptyString()
    {
        Assert.Equal(string.Empty, new CatalogBrand().Brand);
    }

    [Fact]
    public void CatalogType_PropertiesRoundTrip()
    {
        var type = new CatalogType { Id = 4, Type = "USB Memory Stick" };

        Assert.Equal(4, type.Id);
        Assert.Equal("USB Memory Stick", type.Type);
    }

    [Fact]
    public void CatalogType_TypeDefaultsToEmptyString()
    {
        Assert.Equal(string.Empty, new CatalogType().Type);
    }
}
