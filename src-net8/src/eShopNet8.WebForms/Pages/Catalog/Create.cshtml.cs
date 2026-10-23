using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.WebForms.Pages.Catalog
{
    // Port of Catalog/Create.aspx(.cs). Like the legacy Create_Click there is no server-side validation: values are
    // parsed with decimal.Parse/int.Parse (non-numeric -> 500) and an empty name or negative stock is saved as-is.
    public class CreateModel : LegacyPageModel
    {
        public const string CreateButton = "ctl06";

        private readonly ICatalogService catalogService;
        private readonly ILogger<CreateModel> logger;

        public CreateModel(ICatalogService catalogService, ILogger<CreateModel> logger)
        {
            this.catalogService = catalogService;
            this.logger = logger;
        }

        public CatalogItemForm Item { get; private set; }
        public IReadOnlyList<CatalogBrand> Brands { get; private set; }
        public IReadOnlyList<CatalogType> Types { get; private set; }

        public void OnGet()
        {
            LoadLists();
            Item = new CatalogItemForm
            {
                Price = "0.00",
                Stock = "0",
                Restock = "0",
                Maxstock = "0",
            };
        }

        public IActionResult OnPost()
        {
            LoadLists();
            Item = new CatalogItemForm
            {
                Name = Posted("Name"),
                Description = Posted("Description"),
                BrandId = ValidatedSelection(Posted("Brand"), Brands.Select(b => b.Id.ToString()), "Brand"),
                TypeId = ValidatedSelection(Posted("Type"), Types.Select(t => t.Id.ToString()), "Type"),
                Price = Posted("Price"),
                Stock = Posted("Stock"),
                Restock = Posted("Restock"),
                Maxstock = Posted("Maxstock"),
            };

            if (!Clicked(CreateButton))
            {
                return Page();
            }

            var catalogItem = new CatalogItem
            {
                Name = Item.Name,
                Description = Item.Description,
                CatalogBrandId = int.Parse(Item.BrandId),
                CatalogTypeId = int.Parse(Item.TypeId),
                Price = decimal.Parse(Item.Price),
                AvailableStock = int.Parse(Item.Stock),
                RestockThreshold = int.Parse(Item.Restock),
                MaxStockThreshold = int.Parse(Item.Maxstock),
            };

            catalogService.CreateCatalogItem(catalogItem);
            logger.LogInformation("Created catalog item {Id}", catalogItem.Id);
            return RedirectToRoot();
        }

        private void LoadLists()
        {
            Brands = catalogService.GetCatalogBrands().ToList();
            Types = catalogService.GetCatalogTypes().ToList();
        }
    }
}
