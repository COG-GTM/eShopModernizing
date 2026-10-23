using System.ComponentModel.DataAnnotations;
using System.Reflection;
using eShop.Catalog.Core.Models;
using Xunit;

namespace eShop.Catalog.UnitTests
{
    /// <summary>The DataAnnotations on CatalogItem must yield the legacy MVC 5 messages recorded in the baseline.</summary>
    public class CatalogItemValidationTests
    {
        private static CatalogItem Valid() => new CatalogItem
        {
            Name = "Mug", Price = 12.5M, CatalogBrandId = 1, CatalogTypeId = 1,
            AvailableStock = 1, RestockThreshold = 1, MaxStockThreshold = 1,
        };

        private static Dictionary<string, string> Validate(CatalogItem item) =>
            ValidateAll(item).ToDictionary(kv => kv.Key, kv => kv.Value.Single());

        private static Dictionary<string, List<string>> ValidateAll(CatalogItem item)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true);
            return results.GroupBy(r => r.MemberNames.Single()).ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage).ToList());
        }

        private static string DisplayNameOf(string property)
        {
            var display = typeof(CatalogItem).GetProperty(property).GetCustomAttribute<DisplayAttribute>();
            return display?.Name ?? property;
        }

        [Fact]
        public void Valid_item_has_no_errors()
        {
            Assert.Empty(Validate(Valid()));
        }

        [Fact]
        public void New_item_defaults_picture_to_dummy_png()
        {
            Assert.Equal("dummy.png", new CatalogItem().PictureFileName);
            Assert.Equal("dummy.png", CatalogItem.DefaultPictureName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Missing_name_reports_the_required_message(string name)
        {
            var item = Valid();
            item.Name = name;

            Assert.Equal("The Name field is required.", Validate(item)["Name"]);
        }

        [Theory]
        [InlineData("1000001")]
        [InlineData("2000000")]
        public void Price_above_one_million_reports_the_range_message(string price)
        {
            var item = Valid();
            item.Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal("The field Price must be between 0 and 1000000.", Validate(item)["Price"]);
        }

        [Fact]
        public void Range_uses_int_operands_so_fractional_overflow_truncates_like_legacy()
        {
            // [Range(0, 1000000)] has int operands: the decimal is converted with Convert.ToInt32 (same in .NET Framework).
            var item = Valid();
            item.Price = 1000000.01M;

            Assert.False(Validate(item).ContainsKey("Price"));
        }

        [Fact]
        public void Negative_price_reports_both_regex_and_range_messages()
        {
            var item = Valid();
            item.Price = -1;

            Assert.Equal(
                new[] { "The field Price must be a positive number with maximum two decimals.", "The field Price must be between 0 and 1000000." },
                ValidateAll(item)["Price"].OrderByDescending(m => m.Length));
        }

        [Fact]
        public void Price_with_three_decimals_reports_the_regex_message()
        {
            var item = Valid();
            item.Price = 1.234M;

            Assert.Equal("The field Price must be a positive number with maximum two decimals.", Validate(item)["Price"]);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1000000")]
        [InlineData("19.5")]
        [InlineData("8.50")]
        public void Prices_within_range_and_two_decimals_are_valid(string price)
        {
            var item = Valid();
            item.Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

            Assert.False(Validate(item).ContainsKey("Price"));
        }

        [Theory]
        [InlineData(nameof(CatalogItem.AvailableStock), "The field Stock must be between 0 and 10 million.")]
        [InlineData(nameof(CatalogItem.RestockThreshold), "The field Restock must be between 0 and 10 million.")]
        [InlineData(nameof(CatalogItem.MaxStockThreshold), "The field Max stock must be between 0 and 10 million.")]
        public void Stock_fields_out_of_range_report_the_legacy_messages(string property, string message)
        {
            foreach (var value in new[] { -1, 10000001 })
            {
                var item = Valid();
                typeof(CatalogItem).GetProperty(property).SetValue(item, value);

                Assert.Equal(message, Validate(item)[property]);
            }
        }

        [Theory]
        [InlineData(nameof(CatalogItem.AvailableStock))]
        [InlineData(nameof(CatalogItem.RestockThreshold))]
        [InlineData(nameof(CatalogItem.MaxStockThreshold))]
        public void Stock_fields_accept_the_range_bounds(string property)
        {
            foreach (var value in new[] { 0, 10000000 })
            {
                var item = Valid();
                typeof(CatalogItem).GetProperty(property).SetValue(item, value);

                Assert.Empty(Validate(item));
            }
        }

        [Theory]
        [InlineData(nameof(CatalogItem.PictureFileName), "Picture name")]
        [InlineData(nameof(CatalogItem.CatalogTypeId), "Type")]
        [InlineData(nameof(CatalogItem.CatalogType), "Type")]
        [InlineData(nameof(CatalogItem.CatalogBrandId), "Brand")]
        [InlineData(nameof(CatalogItem.CatalogBrand), "Brand")]
        [InlineData(nameof(CatalogItem.AvailableStock), "Stock")]
        [InlineData(nameof(CatalogItem.RestockThreshold), "Restock")]
        [InlineData(nameof(CatalogItem.MaxStockThreshold), "Max stock")]
        [InlineData(nameof(CatalogItem.Name), "Name")]
        [InlineData(nameof(CatalogItem.Price), "Price")]
        public void Display_names_match_the_legacy_labels(string property, string label)
        {
            Assert.Equal(label, DisplayNameOf(property));
        }

        [Fact]
        public void Price_is_annotated_as_currency()
        {
            var dataType = typeof(CatalogItem).GetProperty(nameof(CatalogItem.Price)).GetCustomAttribute<DataTypeAttribute>();

            Assert.Equal(DataType.Currency, dataType.DataType);
        }
    }
}
