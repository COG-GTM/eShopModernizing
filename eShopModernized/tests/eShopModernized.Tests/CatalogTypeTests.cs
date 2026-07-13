using eShopCoreModernized.Models;

namespace eShopModernized.Tests;

public class CatalogTypeTests
{
    [Fact]
    public void Constructor_InitializesTypeToEmptyString()
    {
        var type = new CatalogType();

        Assert.Equal(0, type.Id);
        Assert.Equal(string.Empty, type.Type);
    }

    [Fact]
    public void Properties_RoundTripAssignedValues()
    {
        var type = new CatalogType { Id = 9, Type = "T-Shirt" };

        Assert.Equal(9, type.Id);
        Assert.Equal("T-Shirt", type.Type);
    }
}
