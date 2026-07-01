using eShopCoreModernized.Models;

namespace eShopModernized.Tests.Common;

/// <summary>
/// Factory helpers that build fresh, unattached model instances for tests.
/// Each call returns new objects so that seeding multiple EF contexts does not
/// trigger "instance already tracked" errors.
/// </summary>
public static class TestData
{
    public static List<CatalogBrand> Brands() => new()
    {
        new CatalogBrand { Id = 1, Brand = "Azure" },
        new CatalogBrand { Id = 2, Brand = ".NET" },
        new CatalogBrand { Id = 3, Brand = "Other" }
    };

    public static List<CatalogType> Types() => new()
    {
        new CatalogType { Id = 1, Type = "Mug" },
        new CatalogType { Id = 2, Type = "T-Shirt" }
    };

    public static List<CatalogItem> Items() => new()
    {
        new CatalogItem { Id = 1, Name = "Item One", Description = "First", Price = 10m, PictureFileName = "1.png", CatalogTypeId = 1, CatalogBrandId = 1, AvailableStock = 5 },
        new CatalogItem { Id = 2, Name = "Item Two", Description = "Second", Price = 20m, PictureFileName = "2.png", CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 6 },
        new CatalogItem { Id = 3, Name = "Item Three", Description = "Third", Price = 30m, PictureFileName = "3.png", CatalogTypeId = 1, CatalogBrandId = 3, AvailableStock = 7 }
    };
}
