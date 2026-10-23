using System.Globalization;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopNet8.WebForms.Pages.Catalog;

// Port of Catalog/Create.aspx(.cs).
[IgnoreAntiforgeryToken]
public class CreateModel : PageModel
{
    public const string SubmitButtonName = "ctl06";

    private readonly ICatalogService _catalogService;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(ICatalogService catalogService, IAntiforgery antiforgery, ILogger<CreateModel> logger)
    {
        _catalogService = catalogService;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    public IEnumerable<CatalogBrand> Brands { get; private set; } = Enumerable.Empty<CatalogBrand>();
    public IEnumerable<CatalogType> Types { get; private set; } = Enumerable.Empty<CatalogType>();

    public void OnGet()
    {
        _logger.LogInformation("Now loading... /Catalog/Create.aspx");
        Brands = _catalogService.GetCatalogBrands();
        Types = _catalogService.GetCatalogTypes();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await PostBack.IsButtonClickAsync(HttpContext, _antiforgery, SubmitButtonName))
        {
            OnGet();
            return Page();
        }

        // Legacy checked ModelState.IsValid (always true) instead of Page.IsValid, so the validators never blocked
        // the save: empty names and out-of-range numbers are stored, unparsable numbers throw -> 500 (quirk Q8).
        var form = Request.Form;
        var catalogItem = new CatalogItem
        {
            Name = form["Name"].ToString(),
            Description = form["Description"].ToString(),
            CatalogBrandId = int.Parse(form["Brand"].ToString(), CultureInfo.InvariantCulture),
            CatalogTypeId = int.Parse(form["Type"].ToString(), CultureInfo.InvariantCulture),
            Price = decimal.Parse(form["Price"].ToString(), CultureInfo.InvariantCulture),
            AvailableStock = int.Parse(form["Stock"].ToString(), CultureInfo.InvariantCulture),
            RestockThreshold = int.Parse(form["Restock"].ToString(), CultureInfo.InvariantCulture),
            MaxStockThreshold = int.Parse(form["Maxstock"].ToString(), CultureInfo.InvariantCulture),
        };

        _catalogService.CreateCatalogItem(catalogItem);
        return new LegacyRedirectResult();
    }
}
