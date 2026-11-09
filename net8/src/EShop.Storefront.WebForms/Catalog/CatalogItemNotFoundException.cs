namespace EShop.Storefront.WebForms.Catalog;

/// <summary>Legacy pages dereference a missing item (NullReferenceException, HTTP 500); the Razor Pages throw this instead.</summary>
public sealed class CatalogItemNotFoundException(int id) : Exception($"Catalog item {id} was not found.")
{
    public int Id { get; } = id;
}
