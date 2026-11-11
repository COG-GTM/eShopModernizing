using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.ViewModel;

namespace eShop.Catalog.Data.Services;

/// <summary>Same contract as the legacy eShopLegacyMVC / eShopLegacyWebForms ICatalogService.</summary>
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
