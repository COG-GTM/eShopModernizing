using eShopRazorPages.Models;
using eShopRazorPages.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopRazorPages.Pages.Catalog;

/// <summary>Port of <c>Catalog/Delete.aspx</c>.</summary>
public class DeleteModel : PageModel
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<DeleteModel> _logger;

    public DeleteModel(ICatalogService catalogService, ILogger<DeleteModel> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public CatalogItem Product { get; private set; } = default!;

    public IActionResult OnGet(int id)
    {
        _logger.LogInformation("Now loading... /Catalog/Delete.aspx?id={ProductId}", id);
        var product = _catalogService.FindCatalogItem(id);
        if (product is null)
        {
            return NotFound();
        }

        Product = product;
        return Page();
    }

    public IActionResult OnPost(int id)
    {
        var product = _catalogService.FindCatalogItem(id);
        if (product is null)
        {
            return NotFound();
        }

        _catalogService.RemoveCatalogItem(product);
        return LocalRedirect("~/");
    }
}
