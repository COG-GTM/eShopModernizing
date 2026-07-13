using eShopCoreModernized.Models;

namespace eShopModernized.Tests;

public class CatalogBrandTests
{
    [Fact]
    public void Constructor_InitializesBrandToEmptyString()
    {
        var brand = new CatalogBrand();

        Assert.Equal(0, brand.Id);
        Assert.Equal(string.Empty, brand.Brand);
    }

    [Fact]
    public void Properties_RoundTripAssignedValues()
    {
        var brand = new CatalogBrand { Id = 5, Brand = "Azure" };

        Assert.Equal(5, brand.Id);
        Assert.Equal("Azure", brand.Brand);
    }
}
