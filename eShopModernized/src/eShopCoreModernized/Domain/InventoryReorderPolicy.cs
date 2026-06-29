using eShopCoreModernized.Models;

namespace eShopCoreModernized.Domain
{
    /// <summary>
    /// An item needs reordering when its available stock has dropped to or below
    /// its restock threshold and a reorder is not already in progress.
    /// </summary>
    public class InventoryReorderPolicy : IInventoryReorderPolicy
    {
        public bool RequiresReorder(CatalogItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item.OnReorder)
            {
                return false;
            }

            return item.AvailableStock <= item.RestockThreshold;
        }
    }
}
