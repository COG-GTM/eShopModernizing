using eShopRazorPages.Models;
using eShopRazorPages.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopRazorPages.Pages.Catalog;

/// <summary>Port of <c>Catalog/Details.aspx</c>.</summary>
public class DetailsModel : PageModel
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(ICatalogService catalogService, ILogger<DetailsModel> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public CatalogItem Product { get; private set; } = default!;

    public IActionResult OnGet(int id)
    {
        _logger.LogInformation("Now loading... /Catalog/Details.aspx?id={ProductId}", id);
        var product = _catalogService.FindCatalogItem(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        return Page();
    }
}
