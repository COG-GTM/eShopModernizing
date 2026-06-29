using eShopCoreModernized.Models;
using Xunit;

namespace eShopModernized.Tests.Models
{
    public class CatalogModelsTests
    {
        [Fact]
        public void CatalogItem_DefaultConstructor_UsesDefaultPictureName()
        {
            var item = new CatalogItem();

            Assert.Equal(CatalogItem.DefaultPictureName, item.PictureFileName);
            Assert.Equal("dummy.png", item.PictureFileName);
            Assert.Equal(string.Empty, item.Name);
        }

        [Fact]
        public void CatalogItem_Properties_RoundTrip()
        {
            var brand = new CatalogBrand { Id = 1, Brand = "Azure" };
            var type = new CatalogType { Id = 2, Type = "Mug" };

            var item = new CatalogItem
            {
                Id = 10,
                Name = "Mug",
                Description = "A mug",
                Price = 9.99M,
                PictureFileName = "10.png",
                CatalogBrandId = brand.Id,
                CatalogBrand = brand,
                CatalogTypeId = type.Id,
                CatalogType = type,
                AvailableStock = 5,
                RestockThreshold = 1,
                MaxStockThreshold = 10,
                OnReorder = true,
            };

            Assert.Equal(10, item.Id);
            Assert.Equal("Mug", item.Name);
            Assert.Equal("A mug", item.Description);
            Assert.Equal(9.99M, item.Price);
            Assert.Equal("10.png", item.PictureFileName);
            Assert.Same(brand, item.CatalogBrand);
            Assert.Same(type, item.CatalogType);
            Assert.True(item.OnReorder);
            Assert.Equal(5, item.AvailableStock);
        }

        [Fact]
        public void CatalogBrand_Properties_RoundTrip()
        {
            var brand = new CatalogBrand { Id = 3, Brand = "Visual Studio" };

            Assert.Equal(3, brand.Id);
            Assert.Equal("Visual Studio", brand.Brand);
        }

        [Fact]
        public void CatalogType_Properties_RoundTrip()
        {
            var type = new CatalogType { Id = 4, Type = "T-Shirt" };

            Assert.Equal(4, type.Id);
            Assert.Equal("T-Shirt", type.Type);
        }
    }
}
