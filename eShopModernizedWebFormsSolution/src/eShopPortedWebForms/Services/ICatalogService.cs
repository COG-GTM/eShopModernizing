using eShopPortedWebForms.Models;
using eShopPortedWebForms.ViewModel;

namespace eShopPortedWebForms.Services;

public interface ICatalogService
{
    Task<CatalogItem?> FindCatalogItemAsync(int id);
    Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync();
    Task<PaginatedItemsViewModel<CatalogItem>> GetCatalogItemsPaginatedAsync(int pageSize, int pageIndex);
    Task<IEnumerable<CatalogType>> GetCatalogTypesAsync();
    Task CreateCatalogItemAsync(CatalogItem catalogItem);
    Task UpdateCatalogItemAsync(CatalogItem catalogItem);
    Task RemoveCatalogItemAsync(CatalogItem catalogItem);
}
