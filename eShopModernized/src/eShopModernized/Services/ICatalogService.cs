using eShopModernized.Models;
using eShopModernized.ViewModel;

namespace eShopModernized.Services
{
    public interface ICatalogService : IDisposable
    {
        Task<CatalogItem?> FindCatalogItemAsync(int id);
        CatalogItem? FindCatalogItem(int id);
        Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync();
        IEnumerable<CatalogBrand> GetCatalogBrands();
        Task<PaginatedItemsViewModel<CatalogItem>> GetCatalogItemsPaginatedAsync(int pageSize, int pageIndex);
        PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex);
        Task<IEnumerable<CatalogType>> GetCatalogTypesAsync();
        IEnumerable<CatalogType> GetCatalogTypes();
        Task CreateCatalogItemAsync(CatalogItem catalogItem);
        void CreateCatalogItem(CatalogItem catalogItem);
        Task UpdateCatalogItemAsync(CatalogItem catalogItem);
        void UpdateCatalogItem(CatalogItem catalogItem);
        Task RemoveCatalogItemAsync(CatalogItem catalogItem);
        void RemoveCatalogItem(CatalogItem catalogItem);
    }
}
