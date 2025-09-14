using eShopModernizedCore.Models;
using eShopModernizedCore.ViewModels;

namespace eShopModernizedCore.Services;

public interface ICatalogService : IDisposable
{
    CatalogItem? FindCatalogItem(int id);
    IEnumerable<CatalogBrand> GetCatalogBrands();
    PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex);
    IEnumerable<CatalogType> GetCatalogTypes();
    Task CreateCatalogItemAsync(CatalogItem catalogItem);
    Task UpdateCatalogItemAsync(CatalogItem catalogItem);
    Task RemoveCatalogItemAsync(CatalogItem catalogItem);
}
