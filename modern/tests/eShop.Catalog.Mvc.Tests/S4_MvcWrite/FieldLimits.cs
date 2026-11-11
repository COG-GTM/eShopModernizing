using eShop.Catalog.Data.Services;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

internal static class FieldLimits
{
    public const string NameLength = "The field Name must be a string or array type with a maximum length of &#x27;50&#x27;.";
    public const string PriceRange = "The field Price must be between 0 and 1000000.";

    /// <summary>Every item's editable state, to prove a rejected POST changed nothing.</summary>
    public static string Snapshot(ICatalogService catalog) =>
        string.Join("\n", catalog.GetCatalogItemsPaginated(1000, 0).Data
            .OrderBy(i => i.Id)
            .Select(i => FormattableString.Invariant($"{i.Id}|{i.Name}|{i.Description}|{i.Price}|{i.CatalogBrandId}|{i.CatalogTypeId}|{i.AvailableStock}|{i.RestockThreshold}|{i.MaxStockThreshold}|{i.PictureFileName}")));
}
