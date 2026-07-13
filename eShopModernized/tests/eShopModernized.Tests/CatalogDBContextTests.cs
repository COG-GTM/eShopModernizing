using eShopCoreModernized.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopModernized.Tests;

public class CatalogDBContextTests
{
    private static CatalogDBContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDBContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CatalogDBContext(options);
    }

    [Fact]
    public void DbSets_AreInitialized()
    {
        using var context = CreateInMemoryContext();

        Assert.NotNull(context.CatalogItems);
        Assert.NotNull(context.CatalogBrands);
        Assert.NotNull(context.CatalogTypes);
    }

    [Fact]
    public void AddAndQuery_CatalogItem_PersistsAndRetrievesEntity()
    {
        using var context = CreateInMemoryContext();

        context.CatalogItems.Add(new CatalogItem
        {
            Id = 1,
            Name = "Mug",
            Price = 12M,
            CatalogBrandId = 1,
            CatalogTypeId = 1,
        });
        context.SaveChanges();

        var stored = context.CatalogItems.Single();
        Assert.Equal("Mug", stored.Name);
        Assert.Equal(12M, stored.Price);
    }

    [Fact]
    public void AddAndQuery_CatalogBrandAndType_PersistEntities()
    {
        using var context = CreateInMemoryContext();

        context.CatalogBrands.Add(new CatalogBrand { Id = 1, Brand = "Contoso" });
        context.CatalogTypes.Add(new CatalogType { Id = 1, Type = "Mug" });
        context.SaveChanges();

        Assert.Equal("Contoso", context.CatalogBrands.Single().Brand);
        Assert.Equal("Mug", context.CatalogTypes.Single().Type);
    }

    [Fact]
    public void Model_ConfiguresExpectedTableNames()
    {
        using var context = CreateInMemoryContext();

        Assert.Equal("Catalog", context.Model.FindEntityType(typeof(CatalogItem))!.GetTableName());
        Assert.Equal("CatalogBrand", context.Model.FindEntityType(typeof(CatalogBrand))!.GetTableName());
        Assert.Equal("CatalogType", context.Model.FindEntityType(typeof(CatalogType))!.GetTableName());
    }

    [Fact]
    public void Model_ConfiguresPrimaryKeys()
    {
        using var context = CreateInMemoryContext();

        var itemKey = context.Model.FindEntityType(typeof(CatalogItem))!.FindPrimaryKey();
        Assert.NotNull(itemKey);
        Assert.Equal(nameof(CatalogItem.Id), Assert.Single(itemKey!.Properties).Name);
    }

    [Fact]
    public void Model_ConfiguresRequiredForeignKeysOnCatalogItem()
    {
        using var context = CreateInMemoryContext();
        var itemType = context.Model.FindEntityType(typeof(CatalogItem))!;

        var brandFk = itemType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(CatalogBrand));
        var typeFk = itemType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(CatalogType));

        Assert.True(brandFk.IsRequired);
        Assert.Equal(nameof(CatalogItem.CatalogBrandId), Assert.Single(brandFk.Properties).Name);
        Assert.True(typeFk.IsRequired);
        Assert.Equal(nameof(CatalogItem.CatalogTypeId), Assert.Single(typeFk.Properties).Name);
    }

    [Fact]
    public void Model_IgnoresPictureUriProperty()
    {
        using var context = CreateInMemoryContext();
        var itemType = context.Model.FindEntityType(typeof(CatalogItem))!;

        Assert.Null(itemType.FindProperty(nameof(CatalogItem.PictureUri)));
    }
}
