using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.ViewModel;

namespace eShop.Catalog.Core.Services;

// Same surface as the legacy eShopLegacyMVC.Services.ICatalogService (synchronous, IDisposable).
public interface ICatalogService : IDisposable
{
    CatalogItem? FindCatalogItem(int id);
    IEnumerable<CatalogBrand> GetCatalogBrands();
    PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex);
    IEnumerable<CatalogType> GetCatalogTypes();
    void CreateCatalogItem(CatalogItem catalogItem);
    void UpdateCatalogItem(CatalogItem catalogItem);
    void RemoveCatalogItem(CatalogItem catalogItem);
}
