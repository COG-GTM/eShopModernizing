using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.WebForms.Pages.Catalog
{
    // Port of Catalog/Delete.aspx(.cs): confirmation page, "[ Delete ]" removes the item and redirects to "~".
    public class DeleteModel : LegacyPageModel
    {
        public const string DeleteButton = "ctl11";

        private readonly ICatalogService catalogService;
        private readonly ILogger<DeleteModel> logger;

        public DeleteModel(ICatalogService catalogService, ILogger<DeleteModel> logger)
        {
            this.catalogService = catalogService;
            this.logger = logger;
        }

        public CatalogItem Product { get; private set; }

        public void OnGet() => Load();

        public IActionResult OnPost()
        {
            Load();
            if (!Clicked(DeleteButton))
            {
                return Page();
            }

            logger.LogInformation("Now deleting catalog item {Id}", Product.Id);
            catalogService.RemoveCatalogItem(Product);
            return RedirectToRoot();
        }

        private void Load()
        {
            var productId = RouteInt("id");
            Product = catalogService.FindCatalogItem(productId);
            CatalogPageGuards.EnsureBound(Product, productId);
        }
    }
}
