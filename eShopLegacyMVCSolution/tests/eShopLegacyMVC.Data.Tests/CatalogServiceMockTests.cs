using System.Linq;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Services;
using Xunit;

namespace eShopLegacyMVC.Data.Tests
{
    public class CatalogServiceMockTests
    {
        [Fact]
        public void Pages_are_ordered_by_id_and_composed_with_brand_and_type()
        {
            var service = new CatalogServiceMock();

            var page = service.GetCatalogItemsPaginated(5, 2);

            Assert.Equal(12, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(new[] { 11, 12 }, page.Data.Select(i => i.Id));
            Assert.All(page.Data, i =>
            {
                Assert.Equal(i.CatalogBrandId, i.CatalogBrand.Id);
                Assert.Equal(i.CatalogTypeId, i.CatalogType.Id);
            });
        }

        [Fact]
        public void Create_assigns_max_id_plus_one_and_update_replaces_the_instance()
        {
            var service = new CatalogServiceMock();
            var created = new CatalogItem { Name = "New", CatalogBrandId = 1, CatalogTypeId = 1 };

            service.CreateCatalogItem(created);
            var replacement = new CatalogItem { Id = created.Id, Name = "Renamed", CatalogBrandId = 1, CatalogTypeId = 1 };
            service.UpdateCatalogItem(replacement);

            Assert.Equal(13, created.Id);
            Assert.Same(replacement, service.FindCatalogItem(13));
        }

        [Fact]
        public void Remove_deletes_the_item()
        {
            var service = new CatalogServiceMock();

            service.RemoveCatalogItem(service.FindCatalogItem(1));

            Assert.Null(service.FindCatalogItem(1));
            Assert.Equal(11, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }

        [Fact]
        public void Brands_and_types_come_from_preconfigured_data()
        {
            var service = new CatalogServiceMock();

            Assert.Equal(new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" }, service.GetCatalogBrands().Select(b => b.Brand));
            Assert.Equal(new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" }, service.GetCatalogTypes().Select(t => t.Type));
        }
    }
}
