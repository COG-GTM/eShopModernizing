using System.Globalization;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopNet8.WebForms.Pages.Catalog;

// Port of Catalog/Edit.aspx(.cs).
[IgnoreAntiforgeryToken]
public class EditModel : PageModel
{
    public const string SubmitButtonName = "ctl07";

    private readonly ICatalogService _catalogService;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<EditModel> _logger;

    public EditModel(ICatalogService catalogService, IAntiforgery antiforgery, ILogger<EditModel> logger)
    {
        _catalogService = catalogService;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    public CatalogItem Product { get; private set; } = null!;
    public IEnumerable<CatalogBrand> Brands { get; private set; } = Enumerable.Empty<CatalogBrand>();
    public IEnumerable<CatalogType> Types { get; private set; } = Enumerable.Empty<CatalogType>();
    public string SelectedBrandId { get; private set; } = "";
    public string SelectedTypeId { get; private set; } = "";

    public void OnGet()
    {
        var productId = PostBack.RouteId(RouteData.Values);
        _logger.LogInformation("Now loading... /Catalog/Edit.aspx?id={ProductId}", productId);
        // Unknown id: the legacy page dereferenced the null product -> 500 (quirk Q7).
        Product = _catalogService.FindCatalogItem(productId)!;
        Brands = _catalogService.GetCatalogBrands();
        SelectedBrandId = Product.CatalogBrandId.ToString(CultureInfo.InvariantCulture);
        Types = _catalogService.GetCatalogTypes();
        SelectedTypeId = Product.CatalogTypeId.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await PostBack.IsButtonClickAsync(HttpContext, _antiforgery, SubmitButtonName))
        {
            OnGet();
            return Page();
        }

        // ModelState.IsValid instead of Page.IsValid in the legacy handler: validators are not enforced (quirk Q8).
        var form = Request.Form;
        var catalogItem = new CatalogItem
        {
            Id = PostBack.RouteId(RouteData.Values),
            Name = form["Name"].ToString(),
            Description = form["Description"].ToString(),
            CatalogBrandId = int.Parse(form["BrandDropDownList"].ToString(), CultureInfo.InvariantCulture),
            CatalogTypeId = int.Parse(form["TypeDropDownList"].ToString(), CultureInfo.InvariantCulture),
            Price = decimal.Parse(form["Price"].ToString(), CultureInfo.InvariantCulture),
            PictureFileName = form["PictureFileName"].ToString(),
            AvailableStock = int.Parse(form["Stock"].ToString(), CultureInfo.InvariantCulture),
            RestockThreshold = int.Parse(form["Restock"].ToString(), CultureInfo.InvariantCulture),
            MaxStockThreshold = int.Parse(form["Maxstock"].ToString(), CultureInfo.InvariantCulture),
        };

        _catalogService.UpdateCatalogItem(catalogItem);
        return new LegacyRedirectResult();
    }
}
