using eShopCatalog.Contracts;

namespace eShopCatalogApi.Data;

public static class PreconfiguredData
{
    public static List<CatalogItem> GetPreconfiguredCatalogItems() =>
    [
        new() { Id = 1, CatalogTypeId = 2, CatalogBrandId = 2, Description = ".NET Bot Black Hoodie", Name = ".NET Bot Black Hoodie", Price = 19.5M, Picturefilename = "2.png" },
        new() { Id = 2, CatalogTypeId = 1, CatalogBrandId = 2, Description = ".NET Black & White Mug", Name = ".NET Black & White Mug", Price = 8.50M, Picturefilename = "11.png" },
        new() { Id = 3, CatalogTypeId = 2, CatalogBrandId = 5, Description = "Prism White T-Shirt", Name = "Prism White T-Shirt", Price = 12, Picturefilename = "7.png" },
        new() { Id = 4, CatalogTypeId = 2, CatalogBrandId = 2, Description = ".NET Foundation T-shirt", Name = ".NET Foundation T-shirt", Price = 12, Picturefilename = "5.png" },
        new() { Id = 5, CatalogTypeId = 3, CatalogBrandId = 5, Description = "Roslyn Red Sheet", Name = "Roslyn Red Sheet", Price = 8.5M, Picturefilename = "9.png" },
        new() { Id = 6, CatalogTypeId = 2, CatalogBrandId = 2, Description = ".NET Blue Hoodie", Name = ".NET Blue Hoodie", Price = 12, Picturefilename = "1.png" },
        new() { Id = 7, CatalogTypeId = 2, CatalogBrandId = 5, Description = "Roslyn Red T-Shirt", Name = "Roslyn Red T-Shirt", Price = 12, Picturefilename = "6.png" },
        new() { Id = 8, CatalogTypeId = 2, CatalogBrandId = 5, Description = "Kudu Purple Hoodie", Name = "Kudu Purple Hoodie", Price = 8.5M, Picturefilename = "3.png" },
        new() { Id = 9, CatalogTypeId = 1, CatalogBrandId = 5, Description = "Cup<T> White Mug", Name = "Cup<T> White Mug", Price = 12, Picturefilename = "12.png" },
        new() { Id = 10, CatalogTypeId = 3, CatalogBrandId = 2, Description = ".NET Foundation Sheet", Name = ".NET Foundation Sheet", Price = 12, Picturefilename = "8.png" },
        new() { Id = 11, CatalogTypeId = 3, CatalogBrandId = 2, Description = "Cup<T> Sheet", Name = "Cup<T> Sheet", Price = 8.5M, Picturefilename = "10.png" },
        new() { Id = 12, CatalogTypeId = 2, CatalogBrandId = 5, Description = "Cup<T> TShirt", Name = "Cup<T> TShirt", Price = 12, Picturefilename = "4.png" },
    ];

    public static List<CatalogBrand> GetPreconfiguredCatalogBrands() =>
    [
        new() { Id = 1, Brand = "Azure" },
        new() { Id = 2, Brand = ".NET" },
        new() { Id = 3, Brand = "Visual Studio" },
        new() { Id = 4, Brand = "SQL Server" },
        new() { Id = 5, Brand = "Other" },
    ];

    public static List<DiscountItem> GetPreconfiguredDiscountItems() =>
    [
        new() { Start = new DateTime(2017, 9, 18), End = new DateTime(2017, 9, 21), Size = 0.3f },
        new() { Start = new DateTime(2017, 9, 22), End = new DateTime(2017, 9, 26), Size = 0.25f },
        new() { Start = new DateTime(2017, 9, 27), End = new DateTime(2017, 9, 30), Size = 0.1f },
        new() { Start = new DateTime(2017, 10, 5), End = new DateTime(2017, 10, 20), Size = 0.5f },
        new() { Start = new DateTime(2017, 11, 13), End = new DateTime(2017, 11, 25), Size = 0.3f },
        new() { Start = new DateTime(2017, 12, 20), End = new DateTime(2017, 12, 25), Size = 0.25f },
    ];

    public static List<CatalogType> GetPreconfiguredCatalogTypes() =>
    [
        new() { Id = 1, Type = "Mug" },
        new() { Id = 2, Type = "T-Shirt" },
        new() { Id = 3, Type = "Sheet" },
        new() { Id = 4, Type = "USB Memory Stick" },
    ];

    public static List<CatalogItemsStock> GetPreconfiguredCatalogItemsStock() =>
    [
        new() { StockId = 1, CatalogItemId = 1, Date = new DateTime(2017, 9, 20), AvailableStock = 100 },
        new() { StockId = 2, CatalogItemId = 1, Date = new DateTime(2017, 9, 21), AvailableStock = 120 },
        new() { StockId = 3, CatalogItemId = 1, Date = new DateTime(2017, 9, 22), AvailableStock = 80 },
        new() { StockId = 4, CatalogItemId = 2, Date = new DateTime(2017, 9, 20), AvailableStock = 45 },
        new() { StockId = 5, CatalogItemId = 4, Date = new DateTime(2017, 9, 25), AvailableStock = 65 },
        new() { StockId = 6, CatalogItemId = 5, Date = new DateTime(2017, 9, 28), AvailableStock = 22 },
    ];
}
