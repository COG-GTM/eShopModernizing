using EShop.Catalog.Contracts;
using EShop.Catalog.Data;

namespace EShop.Catalog.Services;

/// <summary>
/// Legacy <c>CatalogServiceMock</c> (singleton, <c>UseMockData=true</c>): in-memory list seeded from <see cref="PreconfiguredData"/>.
/// State lives for the process lifetime, so restarting the app resets the catalog.
/// </summary>
public class CatalogServiceMock : ICatalogService
{
    private readonly object sync = new();
    private readonly List<CatalogItem> catalogItems = PreconfiguredData.GetPreconfiguredCatalogItems();

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        lock (sync)
        {
            var items = ComposeCatalogItems(catalogItems);

            var itemsOnPage = items
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();

            return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, items.Count, itemsOnPage);
        }
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        lock (sync)
        {
            var item = catalogItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.CatalogBrand ??= PreconfiguredData.GetPreconfiguredCatalogBrands().FirstOrDefault(b => b.Id == item.CatalogBrandId);
                item.CatalogType ??= PreconfiguredData.GetPreconfiguredCatalogTypes().FirstOrDefault(t => t.Id == item.CatalogTypeId);
            }

            return item;
        }
    }

    public IEnumerable<CatalogType> GetCatalogTypes() => PreconfiguredData.GetPreconfiguredCatalogTypes();

    public IEnumerable<CatalogBrand> GetCatalogBrands() => PreconfiguredData.GetPreconfiguredCatalogBrands();

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        lock (sync)
        {
            var maxId = catalogItems.Max(i => i.Id);
            catalogItem.Id = ++maxId;
            catalogItems.Add(catalogItem);
        }
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        lock (sync)
        {
            var index = catalogItems.FindIndex(i => i.Id == catalogItem.Id);
            if (index >= 0)
            {
                catalogItems[index] = catalogItem;
            }
        }
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        lock (sync)
        {
            // Legacy List.Remove: a null item (unknown id) is a silent no-op.
            if (catalogItem == null || catalogItems.Remove(catalogItem))
            {
                return;
            }

            catalogItems.RemoveAll(i => i.Id == catalogItem.Id);
        }
    }

    // Legacy ComposeCatalogItems: resolves navigations on the stored items with First(), so an unknown brand/type id throws like legacy.
    private static List<CatalogItem> ComposeCatalogItems(List<CatalogItem> items)
    {
        var catalogTypes = PreconfiguredData.GetPreconfiguredCatalogTypes();
        var catalogBrands = PreconfiguredData.GetPreconfiguredCatalogBrands();
        items.ForEach(i => i.CatalogBrand = catalogBrands.First(b => b.Id == i.CatalogBrandId));
        items.ForEach(i => i.CatalogType = catalogTypes.First(t => t.Id == i.CatalogTypeId));
        return items;
    }
}
