using eShopCoreModernized.Models;

namespace eShopCoreModernized.Services
{
    public interface IBrandService
    {
        Task<IEnumerable<CatalogBrand>> GetBrandsAsync();
        Task<CatalogBrand?> FindBrandAsync(int id);
        Task CreateBrandAsync(CatalogBrand brand);
        Task UpdateBrandAsync(CatalogBrand brand);
        Task RemoveBrandAsync(CatalogBrand brand);
        Task<bool> IsBrandInUseAsync(int id);
    }
}
