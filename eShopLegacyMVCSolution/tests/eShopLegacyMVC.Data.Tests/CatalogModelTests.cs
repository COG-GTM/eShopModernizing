using System.Linq;
using eShopLegacyMVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace eShopLegacyMVC.Data.Tests
{
    public class CatalogModelTests
    {
        [Fact]
        public void Entities_map_to_legacy_table_names()
        {
            using var db = OfflineContext.Create();

            Assert.Equal("Catalog", db.Model.FindEntityType(typeof(CatalogItem)).GetTableName());
            Assert.Equal("CatalogBrand", db.Model.FindEntityType(typeof(CatalogBrand)).GetTableName());
            Assert.Equal("CatalogType", db.Model.FindEntityType(typeof(CatalogType)).GetTableName());
        }

        [Fact]
        public void CatalogItem_key_is_assigned_by_the_application_and_brand_type_keys_are_identity()
        {
            using var db = OfflineContext.Create();

            Assert.Equal(ValueGenerated.Never, db.Model.FindEntityType(typeof(CatalogItem)).FindProperty(nameof(CatalogItem.Id)).ValueGenerated);
            Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, db.Model.FindEntityType(typeof(CatalogBrand)).FindProperty(nameof(CatalogBrand.Id)).GetValueGenerationStrategy());
            Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, db.Model.FindEntityType(typeof(CatalogType)).FindProperty(nameof(CatalogType.Id)).GetValueGenerationStrategy());
        }

        [Fact]
        public void CatalogItem_columns_keep_legacy_facets()
        {
            using var db = OfflineContext.Create();
            var item = db.Model.FindEntityType(typeof(CatalogItem));

            Assert.Equal(50, item.FindProperty(nameof(CatalogItem.Name)).GetMaxLength());
            Assert.False(item.FindProperty(nameof(CatalogItem.Name)).IsNullable);
            Assert.True(item.FindProperty(nameof(CatalogItem.Description)).IsNullable);
            Assert.False(item.FindProperty(nameof(CatalogItem.PictureFileName)).IsNullable);
            Assert.Equal(18, item.FindProperty(nameof(CatalogItem.Price)).GetPrecision());
            Assert.Equal(2, item.FindProperty(nameof(CatalogItem.Price)).GetScale());
            Assert.Null(item.FindProperty(nameof(CatalogItem.PictureUri)));
        }

        [Fact]
        public void Relationships_are_required_and_cascade_like_EF6_HasRequired()
        {
            using var db = OfflineContext.Create();
            var fks = db.Model.FindEntityType(typeof(CatalogItem)).GetForeignKeys().ToList();

            Assert.Equal(2, fks.Count);
            Assert.All(fks, fk =>
            {
                Assert.True(fk.IsRequired);
                Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
            });
        }

        [Fact]
        public void Create_script_contains_hilo_sequences_and_EF6_constraint_names()
        {
            using var db = OfflineContext.Create();
            var script = db.Database.GenerateCreateScript();

            foreach (var sequence in new[] { "catalog_hilo", "catalog_brand_hilo", "catalog_type_hilo" })
            {
                Assert.Contains($"CREATE SEQUENCE [{sequence}] START WITH 1 INCREMENT BY 10", script);
            }

            Assert.Contains("CONSTRAINT [PK_dbo.Catalog] PRIMARY KEY", script);
            Assert.Contains("CONSTRAINT [FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId]", script);
            Assert.Contains("CONSTRAINT [FK_dbo.Catalog_dbo.CatalogType_CatalogTypeId]", script);
            Assert.Contains("CREATE INDEX [IX_CatalogBrandId]", script);
            Assert.Contains("CREATE INDEX [IX_CatalogTypeId]", script);
            Assert.Contains("[Price] decimal(18,2) NOT NULL", script);
        }
    }
}
