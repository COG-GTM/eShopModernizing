using eShop.Catalog.Core.Infrastructure;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Core.ViewModel;

namespace eShop.Catalog.Infrastructure.Mock
{
    // Faithful port of eShopLegacyMVC.Services.CatalogServiceMock (identical in eShopLegacyWebForms), registered as a
    // singleton (legacy Autofac SingleInstance): CRUD persists in-process; ids are max(Id)+1; Update swaps the list
    // element for the posted object, so its CatalogBrand/CatalogType stay null until the next GetCatalogItemsPaginated
    // composes them again; Remove of an unknown/null item is a no-op. The only addition is a lock: legacy relied on
    // a plain List<T> shared by every request, which races (duplicate ids, enumeration errors) under parallel posts.
    public class CatalogServiceMock : ICatalogService
    {
        private readonly object syncRoot = new object();
        private readonly List<CatalogItem> catalogItems;

        public CatalogServiceMock()
        {
            catalogItems = new List<CatalogItem>(PreconfiguredData.GetPreconfiguredCatalogItems());
        }

        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize = 10, int pageIndex = 0)
        {
            lock (syncRoot)
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

        public CatalogItem FindCatalogItem(int id)
        {
            lock (syncRoot)
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
            lock (syncRoot)
            {
                var maxId = catalogItems.Max(i => i.Id);
                catalogItem.Id = ++maxId;
                catalogItems.Add(catalogItem);
            }
        }

        public void UpdateCatalogItem(CatalogItem modifiedItem)
        {
            lock (syncRoot)
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
            lock (syncRoot)
            {
                catalogItems.Remove(catalogItem);
            }
        }

        public void Dispose()
        {
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
}
