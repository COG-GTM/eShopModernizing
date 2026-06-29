using eShopCoreModernized.Models;

namespace eShopCoreModernized.Domain
{
    /// <summary>
    /// Encapsulates the business rule that decides whether a catalog item needs
    /// to be reordered. Kept out of the UI and data-access layers so the rule is
    /// testable and documented in one place.
    /// </summary>
    public interface IInventoryReorderPolicy
    {
        bool RequiresReorder(CatalogItem item);
    }
}
