using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>Shared plumbing for the Create/Edit pages: drop-down data, lookup validation and validator state.</summary>
public abstract class CatalogItemFormPageModel : PageModel
{
    protected CatalogItemFormPageModel(ICatalogService catalogService)
    {
        CatalogService = catalogService;
    }

    public IReadOnlyList<CatalogBrand> Brands { get; private set; } = [];

    public IReadOnlyList<CatalogType> Types { get; private set; } = [];

    /// <summary>Legacy browser-capability split: validators render client-side markup only for script-capable browsers.</summary>
    public bool RenderUplevel => WebFormsBrowserCapabilities.IsUplevel(Request.Headers.UserAgent.ToString());

    protected ICatalogService CatalogService { get; }

    /// <summary>True unless server-side validation failed for the legacy field (e.g. <c>Name</c>).</summary>
    public bool IsFieldValid(string field)
    {
        return !ModelState.TryGetValue(CatalogItemFormInput.FieldNamePrefix + field, out var entry) || entry.Errors.Count == 0;
    }

    /// <summary>WebForms TextBox omits <c>value</c> when the text is empty; Razor drops attributes whose value is null.</summary>
    public static string? AttributeValue(string? text)
    {
        return string.IsNullOrEmpty(text) ? null : text;
    }

    protected void LoadLookups()
    {
        Brands = CatalogService.GetCatalogBrands().ToList();
        Types = CatalogService.GetCatalogTypes().ToList();
    }

    protected (int BrandId, int TypeId) ResolveLookups(CatalogItemFormInput input)
    {
        return (CatalogLookups.Resolve(input.SelectedBrand, Brands.Select(b => b.Id)),
                CatalogLookups.Resolve(input.SelectedType, Types.Select(t => t.Id)));
    }
}
