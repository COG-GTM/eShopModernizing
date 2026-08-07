using eShopCoreModernized.Models;
using eShopCoreModernized.Services;

namespace eShopCoreModernized.Tests
{
    internal sealed class FakeBrandService : IBrandService
    {
        private readonly List<CatalogBrand> brands;
        private readonly HashSet<int> brandsInUse;

        public FakeBrandService(IEnumerable<CatalogBrand>? seed = null, IEnumerable<int>? inUse = null)
        {
            brands = seed?.ToList() ?? new List<CatalogBrand>
            {
                new CatalogBrand { Id = 1, Brand = "Azure" },
                new CatalogBrand { Id = 2, Brand = ".NET" }
            };
            brandsInUse = new HashSet<int>(inUse ?? Enumerable.Empty<int>());
        }

        public Task<IEnumerable<CatalogBrand>> GetBrandsAsync() =>
            Task.FromResult<IEnumerable<CatalogBrand>>(brands.OrderBy(b => b.Id).ToList());

        public Task<CatalogBrand?> FindBrandAsync(int id) =>
            Task.FromResult(brands.FirstOrDefault(b => b.Id == id));

        public Task CreateBrandAsync(CatalogBrand brand)
        {
            brand.Id = brands.Count == 0 ? 1 : brands.Max(b => b.Id) + 1;
            brands.Add(brand);
            return Task.CompletedTask;
        }

        public Task UpdateBrandAsync(CatalogBrand brand)
        {
            var existing = brands.Single(b => b.Id == brand.Id);
            existing.Brand = brand.Brand;
            return Task.CompletedTask;
        }

        public Task RemoveBrandAsync(CatalogBrand brand)
        {
            brands.RemoveAll(b => b.Id == brand.Id);
            return Task.CompletedTask;
        }

        public Task<bool> IsBrandInUseAsync(int id) => Task.FromResult(brandsInUse.Contains(id));

        public IReadOnlyList<CatalogBrand> Brands => brands;
    }
}
