using eShopRazorPages.Models;
using eShopRazorPages.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopRazorPages.Pages.Catalog;

/// <summary>Port of <c>Catalog/Create.aspx</c>.</summary>
public class CreateModel : CatalogFormPageModel
{
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(ICatalogService catalogService, ILogger<CreateModel> logger)
        : base(catalogService)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogInformation("Now loading... /Catalog/Create.aspx");
        LoadLookups();
    }

    public IActionResult OnPost()
    {
        LoadLookups();
        ValidateLookups();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var catalogItem = Input.ApplyTo(new CatalogItem());
        CatalogService.CreateCatalogItem(catalogItem);
        return LocalRedirect("~/");
    }
}
