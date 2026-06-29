using eShopCoreModernized.Models;
using eShopModernized.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eShopModernized.Tests.Models
{
    public class CatalogDBContextTests
    {
        [Fact]
        public void EnsureCreated_BuildsModel_WithConfiguredTableNames()
        {
            using var harness = new SqliteCatalogContext();
            var model = harness.Context.Model;

            Assert.Equal("Catalog", model.FindEntityType(typeof(CatalogItem))!.GetTableName());
            Assert.Equal("CatalogBrand", model.FindEntityType(typeof(CatalogBrand))!.GetTableName());
            Assert.Equal("CatalogType", model.FindEntityType(typeof(CatalogType))!.GetTableName());
        }

        [Fact]
        public void CatalogItem_KeyAndRequiredColumns_AreConfigured()
        {
            using var harness = new SqliteCatalogContext();
            var entity = harness.Context.Model.FindEntityType(typeof(CatalogItem))!;

            Assert.Equal(nameof(CatalogItem.Id), entity.FindPrimaryKey()!.Properties.Single().Name);

            var name = entity.FindProperty(nameof(CatalogItem.Name))!;
            Assert.False(name.IsNullable);
            Assert.Equal(50, name.GetMaxLength());

            Assert.False(entity.FindProperty(nameof(CatalogItem.Price))!.IsNullable);
            Assert.False(entity.FindProperty(nameof(CatalogItem.PictureFileName))!.IsNullable);
        }

        [Fact]
        public void CatalogItem_PictureUri_IsIgnored()
        {
            using var harness = new SqliteCatalogContext();
            var entity = harness.Context.Model.FindEntityType(typeof(CatalogItem))!;

            Assert.Null(entity.FindProperty(nameof(CatalogItem.PictureUri)));
        }

        [Fact]
        public void CatalogItem_HasRequiredForeignKeys_ToBrandAndType()
        {
            using var harness = new SqliteCatalogContext();
            var entity = harness.Context.Model.FindEntityType(typeof(CatalogItem))!;

            var foreignKeys = entity.GetForeignKeys().ToList();
            Assert.Contains(foreignKeys, fk => fk.PrincipalEntityType.ClrType == typeof(CatalogBrand) && fk.IsRequired);
            Assert.Contains(foreignKeys, fk => fk.PrincipalEntityType.ClrType == typeof(CatalogType) && fk.IsRequired);
        }

        [Fact]
        public void Brand_And_Type_HaveMaxLengthConstraints()
        {
            using var harness = new SqliteCatalogContext();
            var model = harness.Context.Model;

            var brand = model.FindEntityType(typeof(CatalogBrand))!.FindProperty(nameof(CatalogBrand.Brand))!;
            var type = model.FindEntityType(typeof(CatalogType))!.FindProperty(nameof(CatalogType.Type))!;

            Assert.Equal(100, brand.GetMaxLength());
            Assert.False(brand.IsNullable);
            Assert.Equal(100, type.GetMaxLength());
            Assert.False(type.IsNullable);
        }

        [Fact]
        public void DbSets_PersistAndQueryThroughRelationalPipeline()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();

            var item = harness.Context.CatalogItems
                .Include(i => i.CatalogBrand)
                .Include(i => i.CatalogType)
                .Single(i => i.Id == 1);

            Assert.Equal(".NET Bot Black Hoodie", item.Name);
            Assert.Equal(".NET", item.CatalogBrand!.Brand);
            Assert.Equal("T-Shirt", item.CatalogType!.Type);
            Assert.Equal(5, harness.Context.CatalogItems.Count());
            Assert.Equal(2, harness.Context.CatalogBrands.Count());
            Assert.Equal(2, harness.Context.CatalogTypes.Count());
        }
    }
}
