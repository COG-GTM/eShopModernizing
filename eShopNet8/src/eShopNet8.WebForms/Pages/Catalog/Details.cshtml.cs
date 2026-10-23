using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopNet8.WebForms.Pages.Catalog;

// Port of Catalog/Details.aspx(.cs).
public class DetailsModel : PageModel
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(ICatalogService catalogService, ILogger<DetailsModel> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public CatalogItem Product { get; private set; } = null!;

    public void OnGet()
    {
        var productId = PostBack.RouteId(RouteData.Values);
        _logger.LogInformation("Now loading... /Catalog/Details.aspx?id={ProductId}", productId);
        // Unknown id or unpopulated navigation properties throw while rendering -> 500, as in legacy (quirks Q4/Q7).
        Product = _catalogService.FindCatalogItem(productId)!;
    }
}
