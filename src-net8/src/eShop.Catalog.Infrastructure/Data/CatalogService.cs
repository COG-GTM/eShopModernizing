using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Core.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShop.Catalog.Infrastructure.Data
{
    // Port of eShopLegacyMVC.Services.CatalogService (SQL mode, UseMockData=false). Registered per scope.
    public class CatalogService : ICatalogService
    {
        private readonly CatalogDBContext db;
        private readonly CatalogItemHiLoGenerator indexGenerator;

        public CatalogService(CatalogDBContext db, CatalogItemHiLoGenerator indexGenerator)
        {
            this.db = db;
            this.indexGenerator = indexGenerator;
        }

        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
        {
            var totalItems = db.CatalogItems.LongCount();

            // SQL Server rejects a negative OFFSET and a non-positive FETCH; the contract wants an empty page there
            // (pageSize == 0 still throws DivideByZeroException in PaginatedItemsViewModel, as in legacy).
            var itemsOnPage = pageIndex < 0 || pageSize <= 0
                ? new List<CatalogItem>()
                : db.CatalogItems
                    .Include(c => c.CatalogBrand)
                    .Include(c => c.CatalogType)
                    .OrderBy(c => c.Id)
                    .Skip(pageSize * pageIndex)
                    .Take(pageSize)
                    .ToList();

            return new PaginatedItemsViewModel<CatalogItem>(
                pageIndex, pageSize, totalItems, itemsOnPage);
        }

        public CatalogItem FindCatalogItem(int id)
        {
            return db.CatalogItems.Include(c => c.CatalogBrand).Include(c => c.CatalogType).FirstOrDefault(ci => ci.Id == id);
        }

        public IEnumerable<CatalogType> GetCatalogTypes()
        {
            return db.CatalogTypes;
        }

        public IEnumerable<CatalogBrand> GetCatalogBrands()
        {
            return db.CatalogBrands;
        }

        public void CreateCatalogItem(CatalogItem catalogItem)
        {
            catalogItem.Id = indexGenerator.GetNextSequenceValue(db);
            db.CatalogItems.Add(catalogItem);
            db.SaveChanges();
        }

        public void UpdateCatalogItem(CatalogItem catalogItem)
        {
            // EF Core refuses to attach a second instance with a key it already tracks (EF6 did too); a page
            // that loaded the item earlier in the same scope would otherwise fail, so drop the stale copy first.
            var tracked = db.ChangeTracker.Entries<CatalogItem>()
                .FirstOrDefault(e => e.Entity.Id == catalogItem.Id && !ReferenceEquals(e.Entity, catalogItem));
            if (tracked != null)
            {
                tracked.State = EntityState.Detached;
            }

            db.Entry(catalogItem).State = EntityState.Modified;
            db.SaveChanges();
        }

        public void RemoveCatalogItem(CatalogItem catalogItem)
        {
            db.CatalogItems.Remove(catalogItem);
            db.SaveChanges();
        }

        public void Dispose()
        {
            db.Dispose();
        }
    }
}
