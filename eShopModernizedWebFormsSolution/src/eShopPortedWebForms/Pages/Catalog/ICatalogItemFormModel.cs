using eShopPortedWebForms.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopPortedWebForms.Pages.Catalog;

public interface ICatalogItemFormModel
{
    CatalogItem Item { get; set; }
    SelectList Brands { get; }
    SelectList Types { get; }
}
