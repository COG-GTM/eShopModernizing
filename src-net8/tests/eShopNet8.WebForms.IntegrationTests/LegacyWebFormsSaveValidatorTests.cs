using eShop.Catalog.Core.Models;
using eShop.Catalog.Infrastructure.Data;
using eShopNet8.WebForms.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eShopNet8.WebForms.IntegrationTests
{
    /// <summary>EF6 ValidateOnSaveEnabled rules of the legacy WebForms CatalogItem (S2-ef-validate-on-save).</summary>
    public class LegacyWebFormsSaveValidatorTests : IClassFixture<WebFormsAppFactory>
    {
        private readonly WebFormsAppFactory factory;
        private readonly LegacyWebFormsSaveValidator validator = new();

        public LegacyWebFormsSaveValidatorTests(WebFormsAppFactory factory)
        {
            this.factory = factory;
        }

        private static CatalogItem Valid() => new()
        {
            Name = "Mug", Price = 12.5M, CatalogBrandId = 1, CatalogTypeId = 1,
            AvailableStock = 1, RestockThreshold = 1, MaxStockThreshold = 1,
        };

        private static CatalogItem With(Action<CatalogItem> change)
        {
            var item = Valid();
            change(item);
            return item;
        }

        public static TheoryData<string, Action<CatalogItem>> Accepted => new()
        {
            { "valid item", _ => { } },
            { "empty name (no [Required] on the WebForms model; EF6 facet allows empty strings)", i => i.Name = "" },
            { "whitespace name", i => i.Name = "   " },
            { "50-char name", i => i.Name = new string('n', 50) },
            { "zero stock values", i => { i.AvailableStock = 0; i.RestockThreshold = 0; i.MaxStockThreshold = 0; } },
            { "10 million stock", i => { i.AvailableStock = 10000000; i.RestockThreshold = 10000000; i.MaxStockThreshold = 10000000; } },
            { "price above the MVC 1,000,000 limit", i => i.Price = 5000000M },
            { "price with two decimals", i => i.Price = 19.50M },
            { "null description", i => i.Description = null },
        };

        public static TheoryData<string, Action<CatalogItem>> Rejected => new()
        {
            { "negative stock", i => i.AvailableStock = -1 },
            { "stock above 10 million", i => i.AvailableStock = 10000001 },
            { "negative restock", i => i.RestockThreshold = -5 },
            { "max stock above 10 million", i => i.MaxStockThreshold = 10000001 },
            { "negative price", i => i.Price = -5M },
            { "price with three decimals", i => i.Price = 1.234M },
            { "null name (EF6 IsRequired facet)", i => i.Name = null },
            { "51-char name (EF6 HasMaxLength(50) facet)", i => i.Name = new string('n', 51) },
            { "null picture file name (EF6 IsRequired facet)", i => i.PictureFileName = null },
        };

        [Theory]
        [MemberData(nameof(Accepted))]
        public void Accepts_what_legacy_ef6_saved(string because, Action<CatalogItem> change)
        {
            var exception = Record.Exception(() => validator.Validate(With(change)));

            Assert.True(exception == null, because);
        }

        [Theory]
        [MemberData(nameof(Rejected))]
        public void Rejects_what_legacy_ef6_refused_to_save(string because, Action<CatalogItem> change)
        {
            var exception = Assert.Throws<CatalogItemValidationException>(() => validator.Validate(With(change)));

            Assert.True(exception.Errors.Count > 0, because);
        }

        [Fact]
        public void Negative_stock_reports_the_legacy_stock_message()
        {
            var exception = Assert.Throws<CatalogItemValidationException>(() => validator.Validate(With(i => i.RestockThreshold = -1)));

            Assert.Equal(new[] { "The field Stock must be between 0 and 10 million." }, exception.Errors);
        }

        [Fact]
        public void WebForms_app_registers_the_legacy_save_validator()
        {
            Assert.IsType<LegacyWebFormsSaveValidator>(factory.Services.GetRequiredService<ICatalogSaveValidator>());
        }
    }
}
