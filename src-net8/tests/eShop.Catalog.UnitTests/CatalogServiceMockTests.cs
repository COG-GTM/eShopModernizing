using eShop.Catalog.Core.Models;
using eShop.Catalog.Infrastructure.Mock;
using Xunit;

namespace eShop.Catalog.UnitTests
{
    public class CatalogServiceMockTests
    {
        [Fact]
        public void Mock_catalog_seeds_twelve_items_like_legacy()
        {
            var service = new CatalogServiceMock();

            var page = service.GetCatalogItemsPaginated(10, 0);

            Assert.Equal(12, page.TotalItems);
            Assert.Equal(2, page.TotalPages);
            Assert.Equal(10, page.Data.Count());
            Assert.Equal(".NET", page.Data.First().CatalogBrand.Brand);
        }

        [Fact]
        public void Pages_are_ordered_by_id_and_skip_page_size_times_index()
        {
            var service = new CatalogServiceMock();

            Assert.Equal(new[] { 11, 12 }, service.GetCatalogItemsPaginated(10, 1).Data.Select(i => i.Id));
            Assert.Equal(new[] { 4, 5, 6 }, service.GetCatalogItemsPaginated(3, 1).Data.Select(i => i.Id));
            Assert.Empty(service.GetCatalogItemsPaginated(10, 5).Data);
            Assert.Equal(Enumerable.Range(1, 12), service.GetCatalogItemsPaginated(100, 0).Data.Select(i => i.Id));
        }

        [Fact]
        public void Page_size_zero_throws_like_legacy()
        {
            Assert.Throws<DivideByZeroException>(() => new CatalogServiceMock().GetCatalogItemsPaginated(0, 0));
        }

        [Fact]
        public void Paginated_list_composes_brand_and_type_navigations()
        {
            var item = new CatalogServiceMock().GetCatalogItemsPaginated(12, 0).Data.Single(i => i.Id == 9);

            Assert.Equal("Cup<T> White Mug", item.Name);
            Assert.Equal("Other", item.CatalogBrand.Brand);
            Assert.Equal("Mug", item.CatalogType.Type);
        }

        [Fact]
        public void Brands_and_types_are_the_preconfigured_lists()
        {
            var service = new CatalogServiceMock();

            Assert.Equal(new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" }, service.GetCatalogBrands().Select(b => b.Brand));
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, service.GetCatalogBrands().Select(b => b.Id));
            Assert.Equal(new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" }, service.GetCatalogTypes().Select(t => t.Type));
        }

        [Fact]
        public void Find_returns_the_item_or_null()
        {
            var service = new CatalogServiceMock();

            Assert.Equal(".NET Bot Black Hoodie", service.FindCatalogItem(1).Name);
            Assert.Equal(19.5M, service.FindCatalogItem(1).Price);
            Assert.Null(service.FindCatalogItem(0));
            Assert.Null(service.FindCatalogItem(13));
            Assert.Null(service.FindCatalogItem(-1));
        }

        [Fact]
        public void Create_assigns_max_id_plus_one()
        {
            var service = new CatalogServiceMock();
            var first = new CatalogItem { Name = "A", CatalogBrandId = 1, CatalogTypeId = 1 };
            var second = new CatalogItem { Name = "B", CatalogBrandId = 1, CatalogTypeId = 1 };

            service.CreateCatalogItem(first);
            service.CreateCatalogItem(second);

            Assert.Equal(13, first.Id);
            Assert.Equal(14, second.Id);
            Assert.Equal(14, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }

        [Fact]
        public void Create_after_removing_the_highest_id_reuses_it()
        {
            var service = new CatalogServiceMock();
            service.RemoveCatalogItem(service.FindCatalogItem(12));
            var item = new CatalogItem { Name = "A", CatalogBrandId = 1, CatalogTypeId = 1 };

            service.CreateCatalogItem(item);

            Assert.Equal(12, item.Id);
        }

        [Fact]
        public void Created_item_has_null_navigations_until_the_next_list_call()
        {
            // Legacy quirk: only GetCatalogItemsPaginated composes CatalogBrand/CatalogType.
            var service = new CatalogServiceMock();
            var item = new CatalogItem { Name = "A", CatalogBrandId = 3, CatalogTypeId = 4 };
            service.CreateCatalogItem(item);

            Assert.Null(service.FindCatalogItem(13).CatalogBrand);
            Assert.Null(service.FindCatalogItem(13).CatalogType);

            service.GetCatalogItemsPaginated(10, 0);

            Assert.Equal("Visual Studio", service.FindCatalogItem(13).CatalogBrand.Brand);
            Assert.Equal("USB Memory Stick", service.FindCatalogItem(13).CatalogType.Type);
        }

        [Fact]
        public void Update_replaces_the_stored_instance_in_place()
        {
            var service = new CatalogServiceMock();
            service.GetCatalogItemsPaginated(10, 0);
            var modified = new CatalogItem { Id = 2, Name = "Renamed", Price = 1, CatalogBrandId = 5, CatalogTypeId = 3 };

            service.UpdateCatalogItem(modified);

            Assert.Same(modified, service.FindCatalogItem(2));
            Assert.Null(service.FindCatalogItem(2).CatalogBrand);
            Assert.Equal(new[] { 1, 2, 3 }, service.GetCatalogItemsPaginated(3, 0).Data.Select(i => i.Id));
            Assert.Equal("Other", service.FindCatalogItem(2).CatalogBrand.Brand);
            Assert.Equal(12, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }

        [Fact]
        public void Update_of_a_missing_id_is_a_no_op()
        {
            var service = new CatalogServiceMock();

            service.UpdateCatalogItem(new CatalogItem { Id = 99, Name = "Ghost" });

            Assert.Null(service.FindCatalogItem(99));
            Assert.Equal(12, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }

        [Fact]
        public void Remove_deletes_by_reference_and_ignores_unknown_instances()
        {
            var service = new CatalogServiceMock();

            service.RemoveCatalogItem(service.FindCatalogItem(5));
            service.RemoveCatalogItem(new CatalogItem { Id = 6 });

            Assert.Null(service.FindCatalogItem(5));
            Assert.NotNull(service.FindCatalogItem(6));
            Assert.Equal(11, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }

        [Fact]
        public void Each_instance_has_its_own_catalog()
        {
            var a = new CatalogServiceMock();
            a.RemoveCatalogItem(a.FindCatalogItem(1));

            Assert.NotNull(new CatalogServiceMock().FindCatalogItem(1));
            a.Dispose();
        }
    }
}
