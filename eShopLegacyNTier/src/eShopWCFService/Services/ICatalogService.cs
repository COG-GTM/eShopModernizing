using eShopWCFService.Models;

namespace eShopWCFService.Services;

public interface ICatalogService : IDisposable
{
    CatalogItem? FindCatalogItem(int id);
    IEnumerable<CatalogBrand> GetCatalogBrands();
    IEnumerable<CatalogItem> GetCatalogItems(int brandIdFilter, int typeIdFilter);
    IEnumerable<CatalogType> GetCatalogTypes();
    int GetAvailableStock(DateTime date, int catalogItemId);
    void CreateAvailableStock(CatalogItemsStock catalogItemsStock);
    void CreateCatalogItem(CatalogItem catalogItem);
    void UpdateCatalogItem(CatalogItem catalogItem);
    void RemoveCatalogItem(CatalogItem catalogItem);
    DiscountItem? GetDiscount(DateTime day);
}
