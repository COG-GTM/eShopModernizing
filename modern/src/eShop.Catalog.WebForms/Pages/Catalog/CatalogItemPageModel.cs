using System.Globalization;
using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>Shared lookup of the legacy Details.aspx / Delete.aspx code-behind (Page.RouteData "id").</summary>
public abstract class CatalogItemPageModel : PageModel
{
    protected CatalogItemPageModel(ICatalogService catalogService, ILogger logger)
    {
        CatalogService = catalogService;
        Logger = logger;
    }

    public CatalogItem Item { get; private set; } = null!;

    protected ICatalogService CatalogService { get; }

    protected ILogger Logger { get; }

    protected abstract string LegacyPagePath { get; }

    /// <summary>
    /// Loads the item for the route id. Legacy throws a NullReferenceException while binding an unknown id,
    /// so the caller answers 500 (same status) when this returns false.
    /// </summary>
    protected bool TryLoadItem(string? id)
    {
        var productId = Convert.ToInt32(id, CultureInfo.InvariantCulture);
        Logger.LogInformation("Now loading... {Page}?id={Id}", LegacyPagePath, productId);

        var item = CatalogService.FindCatalogItem(productId);
        if (item is null)
        {
            return false;
        }

        Item = item;
        return true;
    }

    protected static IActionResult LegacyNotFound() => new StatusCodeResult(StatusCodes.Status500InternalServerError);
}
