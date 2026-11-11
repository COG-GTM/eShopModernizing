using eShop.Catalog.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Data.Tests.S1_Data;

public class CatalogMigrationTests
{
    private static CatalogDbContext Context() => new CatalogDbContextDesignTimeFactory().CreateDbContext(Array.Empty<string>());

    [Fact]
    public void Baseline_migration_script_has_no_hard_coded_database()
    {
        using var db = Context();

        var script = db.GetService<IMigrator>().GenerateScript();

        Assert.DoesNotContain("USE ", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE SEQUENCE [dbo].[catalog_hilo] START WITH 1 INCREMENT BY 10", script);
        Assert.Contains("CREATE SEQUENCE [dbo].[catalog_brand_hilo] START WITH 1 INCREMENT BY 10", script);
        Assert.Contains("CREATE SEQUENCE [dbo].[catalog_type_hilo] START WITH 1 INCREMENT BY 10", script);
        Assert.Contains("CREATE TABLE [dbo].[Catalog]", script);
    }

    [Fact]
    public void Model_has_no_changes_missing_from_migrations()
    {
        using var db = Context();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = db.GetService<IModelRuntimeInitializer>().Initialize((IModel)snapshot, designTime: true);

        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            initialized.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }

    [Fact]
    public void Catalog_id_uses_hilo_block_of_ten_and_lookup_ids_are_identity()
    {
        using var db = Context();
        var model = db.GetService<IDesignTimeModel>().Model;

        var itemId = model.FindEntityType(typeof(Models.CatalogItem))!.FindProperty("Id")!;
        Assert.Equal(SqlServerValueGenerationStrategy.SequenceHiLo, itemId.GetValueGenerationStrategy());
        Assert.Equal("catalog_hilo", itemId.GetHiLoSequenceName());
        Assert.Equal(10, model.FindSequence("catalog_hilo")!.IncrementBy);
        Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn,
            model.FindEntityType(typeof(Models.CatalogBrand))!.FindProperty("Id")!.GetValueGenerationStrategy());
        Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn,
            model.FindEntityType(typeof(Models.CatalogType))!.FindProperty("Id")!.GetValueGenerationStrategy());
    }
}
