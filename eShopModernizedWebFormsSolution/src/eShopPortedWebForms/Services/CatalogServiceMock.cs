using eShopPortedWebForms.Models;
using eShopPortedWebForms.Models.Infrastructure;
using eShopPortedWebForms.ViewModel;

namespace eShopPortedWebForms.Services;

public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> catalogItems;
    private readonly object syncRoot = new();

    public CatalogServiceMock()
    {
        catalogItems = new List<CatalogItem>(PreconfiguredData.GetPreconfiguredCatalogItems());
    }

    public Task<PaginatedItemsViewModel<CatalogItem>> GetCatalogItemsPaginatedAsync(int pageSize, int pageIndex)
    {
        lock (syncRoot)
        {
            var items = ComposeCatalogItems(catalogItems);

            var itemsOnPage = items
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, items.Count, itemsOnPage));
        }
    }

    public Task<CatalogItem?> FindCatalogItemAsync(int id)
    {
        lock (syncRoot)
        {
            var item = catalogItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                ComposeCatalogItems(new List<CatalogItem> { item });
            }
            return Task.FromResult(item);
        }
    }

    public Task<IEnumerable<CatalogType>> GetCatalogTypesAsync()
    {
        return Task.FromResult(PreconfiguredData.GetPreconfiguredCatalogTypes());
    }

    public Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync()
    {
        return Task.FromResult(PreconfiguredData.GetPreconfiguredCatalogBrands());
    }

    public Task CreateCatalogItemAsync(CatalogItem catalogItem)
    {
        lock (syncRoot)
        {
            var maxId = catalogItems.Count == 0 ? 0 : catalogItems.Max(i => i.Id);
            catalogItem.Id = maxId + 1;
            catalogItems.Add(catalogItem);
        }
        return Task.CompletedTask;
    }

    public Task UpdateCatalogItemAsync(CatalogItem modifiedItem)
    {
        lock (syncRoot)
        {
            var originalItem = catalogItems.FirstOrDefault(x => x.Id == modifiedItem.Id);
            if (originalItem != null)
            {
                catalogItems[catalogItems.IndexOf(originalItem)] = modifiedItem;
            }
        }
        return Task.CompletedTask;
    }

    public Task RemoveCatalogItemAsync(CatalogItem catalogItem)
    {
        lock (syncRoot)
        {
            var existing = catalogItems.FirstOrDefault(x => x.Id == catalogItem.Id);
            if (existing != null)
            {
                catalogItems.Remove(existing);
            }
        }
        return Task.CompletedTask;
    }

    private static List<CatalogItem> ComposeCatalogItems(List<CatalogItem> items)
    {
        var catalogTypes = PreconfiguredData.GetPreconfiguredCatalogTypes().ToList();
        var catalogBrands = PreconfiguredData.GetPreconfiguredCatalogBrands().ToList();
        items.ForEach(i => i.CatalogBrand = catalogBrands.First(b => b.Id == i.CatalogBrandId));
        items.ForEach(i => i.CatalogType = catalogTypes.First(t => t.Id == i.CatalogTypeId));
        return items;
    }
}
