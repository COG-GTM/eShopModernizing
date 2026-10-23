using eShop.Catalog.Infrastructure.Mock;
using Xunit;

namespace eShop.Catalog.UnitTests
{
    public class CatalogServiceMockTests
    {
        [Fact]
        public void Seeded_catalog_matches_legacy_first_page()
        {
            var page = new CatalogServiceMock().GetCatalogItemsPaginated(10, 0);

            Assert.Equal(12, page.TotalItems);
            Assert.Equal(2, page.TotalPages);
            Assert.Equal(10, page.Data.Count());
            Assert.Equal(".NET", page.Data.First().CatalogBrand.Brand);
        }
    }
}
