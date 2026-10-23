using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Core.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShop.Catalog.Infrastructure.Data;

// Port of eShopLegacyMVC.Services.CatalogService (EF6). Semantics are kept on purpose (INVENTORY Q6/Q18):
// negative paging reaches SQL Server and fails, Update/Remove of an unknown item throw.
public class CatalogService : ICatalogService
{
    private readonly CatalogDBContext db;
    private readonly ICatalogItemSaveValidator validator;

    public CatalogService(CatalogDBContext db, ICatalogItemSaveValidator validator)
    {
        this.db = db;
        this.validator = validator;
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

    public CatalogItem? FindCatalogItem(int id)
    {
        return db.CatalogItems.Include(c => c.CatalogBrand).Include(c => c.CatalogType).FirstOrDefault(ci => ci.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return db.CatalogTypes.ToList();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return db.CatalogBrands.ToList();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        // Legacy always overwrote the id with the next catalog_hilo value; 0 makes EF Core's HiLo generator assign it.
        catalogItem.Id = 0;
        validator.Validate(catalogItem);
        db.CatalogItems.Add(catalogItem);
        db.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        validator.Validate(catalogItem);
        // The WebForms Edit port reads the stored picture name via FindCatalogItem in the same request scope; drop that
        // tracked copy so the posted item can be attached (legacy read it from ViewState and never loaded the row).
        var tracked = db.CatalogItems.Local.FirstOrDefault(i => i.Id == catalogItem.Id);
        if (tracked is not null && !ReferenceEquals(tracked, catalogItem))
        {
            db.Entry(tracked).State = EntityState.Detached;
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
