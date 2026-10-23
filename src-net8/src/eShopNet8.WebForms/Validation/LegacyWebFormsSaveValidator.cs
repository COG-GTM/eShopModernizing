using System.ComponentModel.DataAnnotations;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Infrastructure.Data;

namespace eShopNet8.WebForms.Validation
{
    /// <summary>
    /// What EF6 validated on SaveChanges in eShopLegacyWebForms (SQL mode): the DataAnnotations of the legacy WebForms
    /// CatalogItem (which, unlike the MVC model, has no [Required] on Name and a wider Price range) plus the validators
    /// EF6 derives from the CatalogDBContext facets (IsRequired -> Required with AllowEmptyStrings, HasMaxLength).
    /// A violation throws, so the postback becomes a 500 and no row is written, as with DbEntityValidationException.
    /// </summary>
    public sealed class LegacyWebFormsSaveValidator : ICatalogSaveValidator
    {
        private const string StockMessage = "The field Stock must be between 0 and 10 million.";

        private static readonly (string Member, string DisplayName, Func<CatalogItem, object> Value, ValidationAttribute[] Rules)[] Rules =
        {
            (nameof(CatalogItem.Name), "Name", i => i.Name, new ValidationAttribute[]
            {
                new RequiredAttribute { AllowEmptyStrings = true },
                new MaxLengthAttribute(50),
            }),
            (nameof(CatalogItem.Price), "Price", i => i.Price, new ValidationAttribute[]
            {
                new RegularExpressionAttribute(@"^\d+(\.\d{0,2})*$") { ErrorMessage = "The field Price must be a positive number with maximum two decimals." },
                new RangeAttribute(0, 9999999999999999.99),
            }),
            (nameof(CatalogItem.PictureFileName), "Picture name", i => i.PictureFileName, new ValidationAttribute[]
            {
                new RequiredAttribute { AllowEmptyStrings = true },
            }),
            (nameof(CatalogItem.AvailableStock), "Stock", i => i.AvailableStock, new ValidationAttribute[]
            {
                new RangeAttribute(0, 10000000) { ErrorMessage = StockMessage },
            }),
            (nameof(CatalogItem.RestockThreshold), "Restock", i => i.RestockThreshold, new ValidationAttribute[]
            {
                new RangeAttribute(0, 10000000) { ErrorMessage = StockMessage },
            }),
            (nameof(CatalogItem.MaxStockThreshold), "Max stock", i => i.MaxStockThreshold, new ValidationAttribute[]
            {
                new RangeAttribute(0, 10000000) { ErrorMessage = StockMessage },
            }),
        };

        public void Validate(CatalogItem catalogItem)
        {
            var results = new List<ValidationResult>();
            foreach (var (member, displayName, value, rules) in Rules)
            {
                var context = new ValidationContext(catalogItem) { MemberName = member, DisplayName = displayName };
                Validator.TryValidateValue(value(catalogItem), context, results, rules);
            }
            if (results.Count > 0)
            {
                throw new CatalogItemValidationException(results.Select(r => r.ErrorMessage).ToList());
            }
        }
    }
}
