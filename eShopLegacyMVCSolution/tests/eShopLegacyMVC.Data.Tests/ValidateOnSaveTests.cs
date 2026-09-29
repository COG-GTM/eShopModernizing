using System.Linq;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Validation;
using Xunit;

namespace eShopLegacyMVC.Data.Tests
{
    public class ValidateOnSaveTests
    {
        private static CatalogItem ValidItem() => new CatalogItem
        {
            Id = 100,
            Name = "Mug",
            Price = 8.5M,
            CatalogBrandId = 1,
            CatalogTypeId = 1,
        };

        private static DbEntityValidationException SaveAndCatch(CatalogDBContext db, object entity)
        {
            db.Add(entity);
            return Assert.Throws<DbEntityValidationException>(() => db.SaveChanges());
        }

        [Fact]
        public void Missing_required_name_fails_before_reaching_the_database()
        {
            using var db = OfflineContext.Create();
            var item = ValidItem();
            item.Name = null;

            var ex = SaveAndCatch(db, item);

            Assert.Equal(DbEntityValidationException.DefaultMessage, ex.Message);
            var error = Assert.Single(Assert.Single(ex.EntityValidationErrors).ValidationErrors);
            Assert.Equal(nameof(CatalogItem.Name), error.PropertyName);
            Assert.Equal("The Name field is required.", error.ErrorMessage);
        }

        [Fact]
        public void Name_longer_than_model_max_length_fails()
        {
            using var db = OfflineContext.Create();
            var item = ValidItem();
            item.Name = new string('x', 51);

            var error = Assert.Single(Assert.Single(SaveAndCatch(db, item).EntityValidationErrors).ValidationErrors);

            Assert.Equal(nameof(CatalogItem.Name), error.PropertyName);
            Assert.Equal("The field Name must be a string or array type with a maximum length of '50'.", error.ErrorMessage);
        }

        [Fact]
        public void Data_annotation_ranges_use_display_names()
        {
            using var db = OfflineContext.Create();
            var item = ValidItem();
            item.Price = 1000001;
            item.AvailableStock = -1;
            item.PictureFileName = null;

            var errors = Assert.Single(SaveAndCatch(db, item).EntityValidationErrors).ValidationErrors
                .ToDictionary(e => e.PropertyName, e => e.ErrorMessage);

            Assert.Equal("The field Price must be between 0 and 1000000.", errors[nameof(CatalogItem.Price)]);
            Assert.Equal("The field Stock must be between 0 and 10 million.", errors[nameof(CatalogItem.AvailableStock)]);
            Assert.Equal("The Picture name field is required.", errors[nameof(CatalogItem.PictureFileName)]);
        }

        [Fact]
        public void Price_with_more_than_two_decimals_fails_the_regular_expression()
        {
            using var db = OfflineContext.Create();
            var item = ValidItem();
            item.Price = 1.234M;

            var error = Assert.Single(Assert.Single(SaveAndCatch(db, item).EntityValidationErrors).ValidationErrors);

            Assert.Equal("The field Price must be a positive number with maximum two decimals.", error.ErrorMessage);
        }

        [Fact]
        public void Brand_and_type_facets_are_validated()
        {
            using var db = OfflineContext.Create();
            db.Add(new CatalogBrand { Brand = new string('b', 101) });
            db.Add(new CatalogType { Type = null });

            var ex = Assert.Throws<DbEntityValidationException>(() => db.SaveChanges());

            var messages = ex.EntityValidationErrors.SelectMany(r => r.ValidationErrors).Select(e => e.ErrorMessage).OrderBy(m => m).ToList();
            Assert.Equal(new[]
            {
                "The field Brand must be a string or array type with a maximum length of '100'.",
                "The Type field is required.",
            }, messages);
        }

        [Fact]
        public void Validation_can_be_disabled_like_EF6_ValidateOnSaveEnabled()
        {
            using var db = OfflineContext.Create();
            db.ValidateOnSaveEnabled = false;
            var item = ValidItem();
            item.Name = null;
            db.Add(item);

            var ex = Record.Exception(() => db.SaveChanges());

            Assert.IsNotType<DbEntityValidationException>(ex);
        }
    }
}
