using eShopCatalog.Contracts;

namespace eShopCatalogApi.Services;

/// <summary>
/// Server-side port of the legacy WCF <c>ICatalogService</c> operation contract.
/// </summary>
public interface ICatalogService
{
    Task<CatalogItem?> FindCatalogItemAsync(int id, CancellationToken cancellationToken = default);
    Task<List<CatalogBrand>> GetCatalogBrandsAsync(CancellationToken cancellationToken = default);
    Task<List<CatalogItem>> GetCatalogItemsAsync(int brandIdFilter, int typeIdFilter, CancellationToken cancellationToken = default);
    Task<List<CatalogType>> GetCatalogTypesAsync(CancellationToken cancellationToken = default);
    Task<int> GetAvailableStockAsync(DateTime date, int catalogItemId, CancellationToken cancellationToken = default);
    Task CreateAvailableStockAsync(CatalogItemsStock catalogItemsStock, CancellationToken cancellationToken = default);
    Task CreateCatalogItemAsync(CatalogItem catalogItem, CancellationToken cancellationToken = default);

    /// <returns><c>false</c> if no item with the given id exists.</returns>
    Task<bool> UpdateCatalogItemAsync(CatalogItem catalogItem, CancellationToken cancellationToken = default);

    /// <returns><c>false</c> if no item with the given id exists.</returns>
    Task<bool> RemoveCatalogItemAsync(int id, CancellationToken cancellationToken = default);

    Task<DiscountItem?> GetDiscountAsync(DateTime day, CancellationToken cancellationToken = default);
}
