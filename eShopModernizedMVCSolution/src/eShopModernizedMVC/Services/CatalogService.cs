using eShopModernizedMVC.Models;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using eShopModernizedMVC.ViewModel;

namespace eShopModernizedMVC.Services
{
    public class CatalogService : ICatalogService
    {
        private CatalogDBContext db;
        private CatalogItemHiLoGenerator indexGenerator;

        public CatalogService(CatalogDBContext db, CatalogItemHiLoGenerator indexGenerator)
        {
            this.db = db;
            this.indexGenerator = indexGenerator;
        }

        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
        {
            return GetCatalogItemsPaginated(pageSize, pageIndex, null, null, null);
        }

        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex, string searchText, int? brandId, int? typeId)
        {
            var query = db.CatalogItems
                .Include(c => c.CatalogBrand)
                .Include(c => c.CatalogType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(c => c.Name.Contains(searchText) || c.Description.Contains(searchText));
            }

            if (brandId.HasValue)
            {
                query = query.Where(c => c.CatalogBrandId == brandId.Value);
            }

            if (typeId.HasValue)
            {
                query = query.Where(c => c.CatalogTypeId == typeId.Value);
            }

            var totalItems = query.LongCount();

            var itemsOnPage = query
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