using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopNet8.WebForms.Pages.Catalog;

// Port of Catalog/Delete.aspx(.cs).
[IgnoreAntiforgeryToken]
public class DeleteModel : PageModel
{
    public const string SubmitButtonName = "ctl11";

    private readonly ICatalogService _catalogService;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<DeleteModel> _logger;

    public DeleteModel(ICatalogService catalogService, IAntiforgery antiforgery, ILogger<DeleteModel> logger)
    {
        _catalogService = catalogService;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    public CatalogItem Product { get; private set; } = null!;

    public void OnGet()
    {
        var productId = PostBack.RouteId(RouteData.Values);
        _logger.LogInformation("Now loading... /Catalog/Delete.aspx?id={ProductId}", productId);
        Product = _catalogService.FindCatalogItem(productId)!;
        // Page_Load data-bound the product on every request (GET and postback): a null product or unpopulated
        // navigation properties throw -> 500 (quirks Q4/Q7).
        _ = Product.CatalogBrand!.Brand + Product.CatalogType!.Type;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        OnGet();
        if (!await PostBack.IsButtonClickAsync(HttpContext, _antiforgery, SubmitButtonName))
        {
            return Page();
        }

        _catalogService.RemoveCatalogItem(Product);
        return new LegacyRedirectResult();
    }
}
