using eShop.Catalog.Core.Models;

namespace eShopNet8.WebForms.Pages.Catalog
{
    internal static class CatalogPageGuards
    {
        // The legacy pages data-bind product.Name etc. on a null product, which surfaces as an HTTP 500.
        public static void EnsureBound(CatalogItem product, int id)
        {
            if (product == null)
            {
                throw new NullReferenceException($"Catalog item {id} was not found.");
            }
        }
    }
}
