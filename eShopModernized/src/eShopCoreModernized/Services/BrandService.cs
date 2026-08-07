using eShopCoreModernized.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopCoreModernized.Services
{
    public class BrandService : IBrandService
    {
        private readonly CatalogDBContext db;
        private readonly IBrandIdGenerator indexGenerator;

        public BrandService(CatalogDBContext db, IBrandIdGenerator indexGenerator)
        {
            this.db = db;
            this.indexGenerator = indexGenerator;
        }

        public async Task<IEnumerable<CatalogBrand>> GetBrandsAsync()
        {
            return await db.CatalogBrands.OrderBy(b => b.Id).ToListAsync();
        }

        public async Task<CatalogBrand?> FindBrandAsync(int id)
        {
            return await db.CatalogBrands.FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task CreateBrandAsync(CatalogBrand brand)
        {
            brand.Id = await indexGenerator.GetNextSequenceValueAsync(db);
            db.CatalogBrands.Add(brand);
            await db.SaveChangesAsync();
        }

        public async Task UpdateBrandAsync(CatalogBrand brand)
        {
            db.Entry(brand).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }

        public async Task RemoveBrandAsync(CatalogBrand brand)
        {
            db.CatalogBrands.Remove(brand);
            await db.SaveChangesAsync();
        }

        public async Task<bool> IsBrandInUseAsync(int id)
        {
            return await db.CatalogItems.AnyAsync(i => i.CatalogBrandId == id);
        }
    }
}
