using eShopModernized.Models;
using eShopModernized.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShopModernized.Services
{
    public class CatalogService : ICatalogService
    {
        private readonly CatalogDBContext db;
        private readonly CatalogItemHiLoGenerator indexGenerator;

        public CatalogService(CatalogDBContext db, CatalogItemHiLoGenerator indexGenerator)
        {
            this.db = db;
            this.indexGenerator = indexGenerator;
        }

        public async Task<PaginatedItemsViewModel<CatalogItem>> GetCatalogItemsPaginatedAsync(int pageSize, int pageIndex)
        {
            var totalItems = await db.CatalogItems.LongCountAsync();

            var itemsOnPage = await db.CatalogItems
                .Include(c => c.CatalogBrand)
                .Include(c => c.CatalogType)
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedItemsViewModel<CatalogItem>(
                pageIndex, pageSize, totalItems, itemsOnPage);
        }

        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
        {
            var totalItems = db.CatalogItems.LongCount();

            var itemsOnPage = db.CatalogItems
                .Include(c => c.CatalogBrand)
                .Include(c => c.CatalogType)
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();

            return new PaginatedItemsViewModel<CatalogItem>(
                pageIndex, pageSize, totalItems, itemsOnPage);
        }

        public async Task<CatalogItem?> FindCatalogItemAsync(int id)
        {
            return await db.CatalogItems
                .Include(c => c.CatalogBrand)
                .Include(c => c.CatalogType)
                .FirstOrDefaultAsync(ci => ci.Id == id);
        }

        public CatalogItem? FindCatalogItem(int id)
        {
            return db.CatalogItems
                .Include(c => c.CatalogBrand)
                .Include(c => c.CatalogType)
                .FirstOrDefault(ci => ci.Id == id);
        }

        public async Task<IEnumerable<CatalogType>> GetCatalogTypesAsync()
        {
            return await db.CatalogTypes.ToListAsync();
        }

        public IEnumerable<CatalogType> GetCatalogTypes()
        {
            return db.CatalogTypes.ToList();
        }

        public async Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync()
        {
            return await db.CatalogBrands.ToListAsync();
        }

        public IEnumerable<CatalogBrand> GetCatalogBrands()
        {
            return db.CatalogBrands.ToList();
        }

        public async Task CreateCatalogItemAsync(CatalogItem catalogItem)
        {
            catalogItem.Id = await indexGenerator.GetNextSequenceValueAsync(db);
            db.CatalogItems.Add(catalogItem);
            await db.SaveChangesAsync();
        }

        public void CreateCatalogItem(CatalogItem catalogItem)
        {
            catalogItem.Id = indexGenerator.GetNextSequenceValue(db);
            db.CatalogItems.Add(catalogItem);
            db.SaveChanges();
        }

        public async Task UpdateCatalogItemAsync(CatalogItem catalogItem)
        {
            db.Entry(catalogItem).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }

        public void UpdateCatalogItem(CatalogItem catalogItem)
        {
            db.Entry(catalogItem).State = EntityState.Modified;
            db.SaveChanges();
        }

        public async Task RemoveCatalogItemAsync(CatalogItem catalogItem)
        {
            db.CatalogItems.Remove(catalogItem);
            await db.SaveChangesAsync();
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
