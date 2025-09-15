namespace eShopCoreAPI.ViewModels;

public class PaginatedItemsViewModel<TEntity> where TEntity : class
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public long Count { get; set; }
    public IEnumerable<TEntity> Data { get; set; } = Enumerable.Empty<TEntity>();
}
