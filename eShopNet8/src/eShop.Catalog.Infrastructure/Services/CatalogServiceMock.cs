using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Seed;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Core.ViewModel;

namespace eShop.Catalog.Infrastructure.Services;

// Port of eShopLegacyMVC/eShopLegacyWebForms Services.CatalogServiceMock (registered as a singleton when UseMockData=true).
// Legacy semantics are kept on purpose (INVENTORY sections 3/10):
// - Find returns the stored instance; CatalogBrand/CatalogType are only composed onto stored items by
//   GetCatalogItemsPaginated (Q4: a "cold" Details/Edit/Delete shows empty Brand/Type until a list page was rendered).
// - Create assigns max(Id)+1; Update replaces the stored element by Id (unknown Id: no-op);
//   Remove removes the given instance (unknown/null: no-op).
// The only addition is a lock so concurrent requests cannot corrupt the list or hand out duplicate ids.
public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> catalogItems;
    private readonly object sync = new();

    public CatalogServiceMock()
    {
        catalogItems = new List<CatalogItem>(PreconfiguredData.GetPreconfiguredCatalogItems());
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize = 10, int pageIndex = 0)
    {
        List<CatalogItem> itemsOnPage;
        int count;
        lock (sync)
        {
            var items = ComposeCatalogItems(catalogItems);

            itemsOnPage = items
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();
            count = items.Count;
        }

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, count, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        lock (sync)
        {
            return catalogItems.FirstOrDefault(x => x.Id == id);
        }
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return PreconfiguredData.GetPreconfiguredCatalogTypes();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return PreconfiguredData.GetPreconfiguredCatalogBrands();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        lock (sync)
        {
            var maxId = catalogItems.Max(i => i.Id);
            catalogItem.Id = ++maxId;
            catalogItems.Add(catalogItem);
        }
    }

    public void UpdateCatalogItem(CatalogItem modifiedItem)
    {
        lock (sync)
        {
            var index = catalogItems.FindIndex(x => x.Id == modifiedItem.Id);
            if (index >= 0)
            {
                catalogItems[index] = modifiedItem;
            }
        }
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        lock (sync)
        {
            catalogItems.Remove(catalogItem);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static List<CatalogItem> ComposeCatalogItems(List<CatalogItem> items)
    {
        var catalogTypes = PreconfiguredData.GetPreconfiguredCatalogTypes();
        var catalogBrands = PreconfiguredData.GetPreconfiguredCatalogBrands();
        items.ForEach(i => i.CatalogBrand = catalogBrands.First(b => b.Id == i.CatalogBrandId));
        items.ForEach(i => i.CatalogType = catalogTypes.First(b => b.Id == i.CatalogTypeId));

        return items;
    }
}
