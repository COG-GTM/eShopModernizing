using Catalog.Api.Models;
using Catalog.Api.ViewModel;

namespace Catalog.Api.Services;

// Ported from eShopLegacyMVCSolution/src/eShopLegacyMVC/Services/ICatalogService.cs.
// IDisposable removed: lifetime is now managed by the ASP.NET Core DI container.
public interface ICatalogService
{
    CatalogItem? FindCatalogItem(int id);
    IEnumerable<CatalogBrand> GetCatalogBrands();
    PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex);
    IEnumerable<CatalogType> GetCatalogTypes();
    CatalogItem CreateCatalogItem(CatalogItem catalogItem);
    void UpdateCatalogItem(CatalogItem catalogItem);
    void RemoveCatalogItem(CatalogItem catalogItem);
}
