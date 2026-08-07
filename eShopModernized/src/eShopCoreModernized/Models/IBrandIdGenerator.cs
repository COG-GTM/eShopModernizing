namespace eShopCoreModernized.Models
{
    public interface IBrandIdGenerator
    {
        Task<int> GetNextSequenceValueAsync(CatalogDBContext db);
    }
}
