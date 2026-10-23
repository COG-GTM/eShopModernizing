using System.ComponentModel.DataAnnotations;
using eShop.Catalog.Core.Models;

namespace eShop.Catalog.Infrastructure.Data;

// EF6 validated Added/Modified entities on SaveChanges against each legacy app's own CatalogItem annotations and threw
// DbEntityValidationException (-> 500, nothing saved). EF Core does not, so the EF CatalogService runs these rules first.
// The legacy MVC and WebForms models carry different annotations, so each app can register its own rules.
public interface ICatalogItemSaveValidator
{
    void Validate(CatalogItem catalogItem);
}

// eShopLegacyMVC.Models.CatalogItem annotations (copied verbatim onto eShop.Catalog.Core.Models.CatalogItem).
public sealed class CatalogItemAnnotationsSaveValidator : ICatalogItemSaveValidator
{
    public void Validate(CatalogItem catalogItem) =>
        Validator.ValidateObject(catalogItem, new ValidationContext(catalogItem), validateAllProperties: true);
}
