using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>Port of legacy Catalog/Details.aspx.</summary>
public class DetailsModel : CatalogItemPageModel
{
    public DetailsModel(ICatalogService catalogService, ILogger<DetailsModel> logger)
        : base(catalogService, logger)
    {
    }

    protected override string LegacyPagePath => "/Catalog/Details.aspx";

    public IActionResult OnGet(string? id) => TryLoadItem(id) ? Page() : LegacyNotFound();
}
