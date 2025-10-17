using System.Collections.Generic;

namespace eShopModernizedWebForms.Models.Infrastructure
{
    public static class PreconfiguredData
    {
        public static List<CatalogItem> GetPreconfiguredCatalogItems()
        {
            return new List<CatalogItem>()
            {
                new CatalogItem { Id =1, CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Bot Black Hoodie", Name = ".NET Bot Black Hoodie", Price = 19.5M, PictureFileName = "1.png" },
                new CatalogItem { Id =2, CatalogTypeId = 1, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Black & White Mug", Name = ".NET Black & White Mug", Price= 8.50M, PictureFileName = "2.png" },
                new CatalogItem { Id =3, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Prism White T-Shirt", Name = "Prism White T-Shirt", Price = 12, PictureFileName = "3.png" },
                new CatalogItem { Id =4, CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Foundation T-shirt", Name = ".NET Foundation T-shirt", Price = 12, PictureFileName = "4.png" },
                new CatalogItem { Id =5, CatalogTypeId = 3, CatalogBrandId = 5, AvailableStock = 100, Description = "Roslyn Red Sheet", Name = "Roslyn Red Sheet", Price = 8.5M, PictureFileName = "5.png" },
                new CatalogItem { Id =6, CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Blue Hoodie", Name = ".NET Blue Hoodie", Price = 12, PictureFileName = "6.png" },
                new CatalogItem { Id =7, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Roslyn Red T-Shirt", Name = "Roslyn Red T-Shirt", Price = 12, PictureFileName = "7.png" },
                new CatalogItem { Id =8, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Kudu Purple Hoodie", Name = "Kudu Purple Hoodie", Price = 8.5M, PictureFileName = "8.png" },
                new CatalogItem { Id =9, CatalogTypeId = 1, CatalogBrandId = 5, AvailableStock = 100, Description = "Cup<T> White Mug", Name = "Cup<T> White Mug", Price = 12, PictureFileName = "9.png" },
                new CatalogItem { Id =10, CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Foundation Sheet", Name = ".NET Foundation Sheet", Price = 12, PictureFileName = "10.png" },
                new CatalogItem { Id =11, CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 100, Description = "Cup<T> Sheet", Name = "Cup<T> Sheet", Price = 8.5M, PictureFileName = "11.png" },
                new CatalogItem { Id =12, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Prism White TShirt", Name = "Prism White TShirt", Price = 12, PictureFileName = "12.png" },
                new CatalogItem { Id =13, CatalogTypeId = 4, CatalogBrandId = 1, AvailableStock = 100, Description = "Azure Logo USB Memory Stick", Name = "Azure Logo USB Memory Stick", Price = 15.5M, PictureFileName = "13.png" },
                new CatalogItem { Id =14, CatalogTypeId = 1, CatalogBrandId = 3, AvailableStock = 100, Description = "Visual Studio Coffee Mug", Name = "Visual Studio Coffee Mug", Price = 10M, PictureFileName = "14.png" },
                new CatalogItem { Id =15, CatalogTypeId = 2, CatalogBrandId = 4, AvailableStock = 100, Description = "SQL Server Gray T-Shirt", Name = "SQL Server Gray T-Shirt", Price = 14M, PictureFileName = "15.png" },
                new CatalogItem { Id =16, CatalogTypeId = 3, CatalogBrandId = 1, AvailableStock = 100, Description = "Azure Blue Sheet", Name = "Azure Blue Sheet", Price = 9M, PictureFileName = "16.png" },
                new CatalogItem { Id =17, CatalogTypeId = 2, CatalogBrandId = 3, AvailableStock = 100, Description = "Visual Studio Purple T-Shirt", Name = "Visual Studio Purple T-Shirt", Price = 13M, PictureFileName = "17.png" },
            };
        }

        public static IEnumerable<CatalogBrand> GetPreconfiguredCatalogBrands()
        {
            return new List<CatalogBrand>()
            {
                new CatalogBrand() { Id =1, Brand = "Azure"},
                new CatalogBrand() { Id =2, Brand = ".NET" },
                new CatalogBrand() { Id =3, Brand = "Visual Studio" },
                new CatalogBrand() { Id =4, Brand = "SQL Server" },
                new CatalogBrand() { Id =5, Brand = "Other" }
            };
        }

        public static IEnumerable<CatalogType> GetPreconfiguredCatalogTypes()
        {
            return new List<CatalogType>()
            {
                new CatalogType() { Id =1, Type = "Mug"},
                new CatalogType() { Id =2, Type = "T-Shirt" },
                new CatalogType() { Id =3, Type = "Sheet" },
                new CatalogType() { Id =4, Type = "USB Memory Stick" }
            };
        }
    }
}
