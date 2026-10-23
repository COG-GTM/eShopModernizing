using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;

namespace eShopNet8.WebForms.Pages.Catalog
{
    // Port of Catalog/Details.aspx(.cs). A missing item throws on bind (NullReferenceException -> 500) like the legacy page.
    public class DetailsModel : LegacyPageModel
    {
        private readonly ICatalogService catalogService;

        public DetailsModel(ICatalogService catalogService)
        {
            this.catalogService = catalogService;
        }

        public CatalogItem Product { get; private set; }

        public void OnGet() => Load();

        public void OnPost() => Load();

        private void Load()
        {
            var productId = RouteInt("id");
            Product = catalogService.FindCatalogItem(productId);
            CatalogPageGuards.EnsureBound(Product, productId);
        }
    }
}
