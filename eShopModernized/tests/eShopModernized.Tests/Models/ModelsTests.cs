using eShopCoreModernized.Models;
using Xunit;

namespace eShopModernized.Tests.Models;

public class ModelsTests
{
    [Fact]
    public void CatalogBrand_StoresProperties()
    {
        var brand = new CatalogBrand { Id = 3, Brand = "Contoso" };

        Assert.Equal(3, brand.Id);
        Assert.Equal("Contoso", brand.Brand);
    }

    [Fact]
    public void CatalogBrand_Brand_DefaultsToEmpty()
    {
        Assert.Equal(string.Empty, new CatalogBrand().Brand);
    }

    [Fact]
    public void CatalogType_StoresProperties()
    {
        var type = new CatalogType { Id = 2, Type = "Mug" };

        Assert.Equal(2, type.Id);
        Assert.Equal("Mug", type.Type);
    }

    [Fact]
    public void CatalogType_Type_DefaultsToEmpty()
    {
        Assert.Equal(string.Empty, new CatalogType().Type);
    }

    [Fact]
    public void CatalogItem_Constructor_SetsDefaultPictureName()
    {
        Assert.Equal(CatalogItem.DefaultPictureName, new CatalogItem().PictureFileName);
        Assert.Equal("dummy.png", CatalogItem.DefaultPictureName);
    }

    [Fact]
    public void CatalogItem_StoresAllProperties()
    {
        var brand = new CatalogBrand { Id = 1, Brand = "Azure" };
        var type = new CatalogType { Id = 1, Type = "Mug" };

        var item = new CatalogItem
        {
            Id = 10,
            Name = "Widget",
            Description = "A widget",
            Price = 42.50m,
            PictureFileName = "10.png",
            PictureUri = "/pics/10.png",
            TempImageName = "/pics/temp/10.png",
            CatalogTypeId = 1,
            CatalogType = type,
            CatalogBrandId = 1,
            CatalogBrand = brand,
            AvailableStock = 100,
            RestockThreshold = 5,
            MaxStockThreshold = 200,
            OnReorder = true
        };

        Assert.Equal(10, item.Id);
        Assert.Equal("Widget", item.Name);
        Assert.Equal("A widget", item.Description);
        Assert.Equal(42.50m, item.Price);
        Assert.Equal("10.png", item.PictureFileName);
        Assert.Equal("/pics/10.png", item.PictureUri);
        Assert.Equal("/pics/temp/10.png", item.TempImageName);
        Assert.Equal(1, item.CatalogTypeId);
        Assert.Same(type, item.CatalogType);
        Assert.Equal(1, item.CatalogBrandId);
        Assert.Same(brand, item.CatalogBrand);
        Assert.Equal(100, item.AvailableStock);
        Assert.Equal(5, item.RestockThreshold);
        Assert.Equal(200, item.MaxStockThreshold);
        Assert.True(item.OnReorder);
    }
}
