using System;
using System.Collections.Generic;
using System.Linq;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShopLegacyMVC.Services
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
            // EF6 refused to attach a graph whose navigation keys disagree with the FK values; EF Core would silently
            // let the FK win.
            if (catalogItem != null && db.Entry(catalogItem).State == EntityState.Detached)
            {
                ThrowIfReferenceConflicts(catalogItem.CatalogBrand?.Id, catalogItem.CatalogBrandId, "CatalogBrand.Id", "CatalogItem.CatalogBrandId");
                ThrowIfReferenceConflicts(catalogItem.CatalogType?.Id, catalogItem.CatalogTypeId, "CatalogType.Id", "CatalogItem.CatalogTypeId");
            }

            db.Entry(catalogItem).State = EntityState.Modified;
            db.SaveChanges();
        }

        public void RemoveCatalogItem(CatalogItem catalogItem)
        {
            // EF6's DbSet.Remove only accepted tracked entities; EF Core would silently attach and delete.
            if (catalogItem != null && db.Entry(catalogItem).State == EntityState.Detached)
            {
                throw new InvalidOperationException(
                    "The object cannot be deleted because it was not found in the ObjectStateManager.");
            }

            db.CatalogItems.Remove(catalogItem);
            db.SaveChanges();
        }

        private static void ThrowIfReferenceConflicts(int? principalKey, int foreignKey, string principalProperty, string dependentProperty)
        {
            if (principalKey.HasValue && principalKey.Value != foreignKey)
            {
                throw new InvalidOperationException(
                    $"A referential integrity constraint violation occurred: The property value(s) of '{principalProperty}' on one end of a relationship do not match the property value(s) of '{dependentProperty}' on the other end.");
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }
    }
}
