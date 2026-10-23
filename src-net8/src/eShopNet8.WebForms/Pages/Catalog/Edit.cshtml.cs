using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.WebForms.Pages.Catalog
{
    // Port of Catalog/Edit.aspx(.cs). Save_Click parses the posted values without server-side validation and
    // replaces the item; PictureFileName is a ReadOnly TextBox, whose posted value WebForms ignores (it keeps the
    // value bound on the GET), so the stored file name is kept here too.
    public class EditModel : LegacyPageModel
    {
        public const string SaveButton = "ctl07";

        private readonly ICatalogService catalogService;
        private readonly ILogger<EditModel> logger;

        public EditModel(ICatalogService catalogService, ILogger<EditModel> logger)
        {
            this.catalogService = catalogService;
            this.logger = logger;
        }

        public int ProductId { get; private set; }
        public CatalogItemForm Item { get; private set; }
        public IReadOnlyList<CatalogBrand> Brands { get; private set; }
        public IReadOnlyList<CatalogType> Types { get; private set; }

        public void OnGet()
        {
            var product = Load();
            Item = CatalogItemForm.From(product);
        }

        public IActionResult OnPost()
        {
            var product = Load();
            Item = new CatalogItemForm
            {
                Name = Posted("Name"),
                Description = Posted("Description"),
                BrandId = ValidatedSelection(Posted("BrandDropDownList"), Brands.Select(b => b.Id.ToString()), "BrandDropDownList"),
                TypeId = ValidatedSelection(Posted("TypeDropDownList"), Types.Select(t => t.Id.ToString()), "TypeDropDownList"),
                Price = Posted("Price"),
                PictureFileName = product.PictureFileName,
                Stock = Posted("Stock"),
                Restock = Posted("Restock"),
                Maxstock = Posted("Maxstock"),
            };

            if (!Clicked(SaveButton))
            {
                return Page();
            }

            var catalogItem = new CatalogItem
            {
                Id = ProductId,
                Name = Item.Name,
                Description = Item.Description,
                CatalogBrandId = int.Parse(Item.BrandId),
                CatalogTypeId = int.Parse(Item.TypeId),
                Price = decimal.Parse(Item.Price),
                PictureFileName = Item.PictureFileName,
                AvailableStock = int.Parse(Item.Stock),
                RestockThreshold = int.Parse(Item.Restock),
                MaxStockThreshold = int.Parse(Item.Maxstock),
            };

            catalogService.UpdateCatalogItem(catalogItem);
            logger.LogInformation("Updated catalog item {Id}", catalogItem.Id);
            return RedirectToRoot();
        }

        private CatalogItem Load()
        {
            ProductId = RouteInt("id");
            var product = catalogService.FindCatalogItem(ProductId);
            CatalogPageGuards.EnsureBound(product, ProductId);
            Brands = catalogService.GetCatalogBrands().ToList();
            Types = catalogService.GetCatalogTypes().ToList();
            return product;
        }
    }
}
