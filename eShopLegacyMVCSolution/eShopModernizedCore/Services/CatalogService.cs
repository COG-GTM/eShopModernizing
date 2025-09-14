using eShopModernizedCore.Models;
using eShopModernizedCore.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace eShopModernizedCore.Services;

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _context;
    private readonly CatalogItemHiLoGenerator _idGenerator;

    public CatalogService(CatalogDbContext context, CatalogItemHiLoGenerator idGenerator)
    {
        _context = context;
        _idGenerator = idGenerator;
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = _context.CatalogItems.LongCount();

        var itemsOnPage = _context.CatalogItems
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
        return _context.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .FirstOrDefault(ci => ci.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _context.CatalogTypes.ToList();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _context.CatalogBrands.ToList();
    }

    public async Task CreateCatalogItemAsync(CatalogItem catalogItem)
    {
        catalogItem.Id = await _idGenerator.GetNextSequenceValueAsync(_context);
        _context.CatalogItems.Add(catalogItem);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCatalogItemAsync(CatalogItem catalogItem)
    {
        _context.Entry(catalogItem).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task RemoveCatalogItemAsync(CatalogItem catalogItem)
    {
        _context.CatalogItems.Remove(catalogItem);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
