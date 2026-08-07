using eShopCoreModernized.Models;

namespace eShopCoreModernized.Services
{
    public class BrandServiceMock : IBrandService
    {
        private readonly ICatalogService catalogService;
        private readonly List<CatalogBrand> brands;

        public BrandServiceMock(ICatalogService catalogService)
        {
            this.catalogService = catalogService;

            // CatalogServiceMock hands back its live brand collection, so brand edits made
            // here stay visible on the catalog item screens while running on mock data.
            brands = catalogService.GetCatalogBrands() as List<CatalogBrand>
                ?? catalogService.GetCatalogBrands().ToList();
        }

        public Task<IEnumerable<CatalogBrand>> GetBrandsAsync()
        {
            return Task.FromResult<IEnumerable<CatalogBrand>>(brands.OrderBy(b => b.Id).ToList());
        }

        public Task<CatalogBrand?> FindBrandAsync(int id)
        {
            return Task.FromResult(brands.FirstOrDefault(b => b.Id == id));
        }

        public Task CreateBrandAsync(CatalogBrand brand)
        {
            brand.Id = brands.Count == 0 ? 1 : brands.Max(b => b.Id) + 1;
            brands.Add(brand);
            return Task.CompletedTask;
        }

        public Task UpdateBrandAsync(CatalogBrand brand)
        {
            var existing = brands.FirstOrDefault(b => b.Id == brand.Id);
            if (existing != null)
            {
                existing.Brand = brand.Brand;
            }
            return Task.CompletedTask;
        }

        public Task RemoveBrandAsync(CatalogBrand brand)
        {
            brands.RemoveAll(b => b.Id == brand.Id);
            return Task.CompletedTask;
        }

        public Task<bool> IsBrandInUseAsync(int id)
        {
            var items = catalogService.GetCatalogItemsPaginated(int.MaxValue, 0);
            return Task.FromResult(items.Data.Any(i => i.CatalogBrandId == id));
        }
    }
}
