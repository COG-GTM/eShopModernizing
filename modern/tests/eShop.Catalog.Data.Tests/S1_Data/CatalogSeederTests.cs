using System.IO.Compression;
using eShop.Catalog.Data.Seeding;
using Microsoft.EntityFrameworkCore;
using static eShop.Catalog.Data.Tests.S1_Data.TestSupport;

namespace eShop.Catalog.Data.Tests.S1_Data;

public class CatalogSeederTests
{
    [Fact]
    public async Task Seeds_types_then_brands_then_items_in_separate_saves()
    {
        var order = new SaveOrderInterceptor();
        await using var db = InMemoryContext(interceptors: order);

        await Seeder().SeedAsync(db);

        Assert.Equal(new[] { new[] { "CatalogType" }, new[] { "CatalogBrand" }, new[] { "CatalogItem" } }, order.Batches);
    }

    [Fact]
    public async Task Seeds_legacy_preconfigured_data_with_generated_ids()
    {
        await using var db = InMemoryContext();

        await Seeder().SeedAsync(db);

        Assert.Equal(new[] { (1, "Mug"), (2, "T-Shirt"), (3, "Sheet"), (4, "USB Memory Stick") },
            await db.CatalogTypes.OrderBy(t => t.Id).Select(t => ValueTuple.Create(t.Id, t.Type)).ToListAsync());
        Assert.Equal(new[] { (1, "Azure"), (2, ".NET"), (3, "Visual Studio"), (4, "SQL Server"), (5, "Other") },
            await db.CatalogBrands.OrderBy(b => b.Id).Select(b => ValueTuple.Create(b.Id, b.Brand)).ToListAsync());

        var items = await db.CatalogItems.OrderBy(i => i.Id).ToListAsync();
        var expected = PreconfiguredData.GetPreconfiguredCatalogItems();
        Assert.Equal(expected.Select(e => (e.Id, e.Name, e.Description, e.Price, e.PictureFileName, e.CatalogTypeId, e.CatalogBrandId, e.AvailableStock)),
            items.Select(i => (i.Id, i.Name, i.Description, i.Price, i.PictureFileName, i.CatalogTypeId, i.CatalogBrandId, i.AvailableStock)));
    }

    [Fact]
    public async Task Is_idempotent()
    {
        var name = Guid.NewGuid().ToString("N");
        await using (var db = InMemoryContext(name))
        {
            await Seeder().SeedAsync(db);
        }

        var order = new SaveOrderInterceptor();
        await using (var db = InMemoryContext(name, order))
        {
            await Seeder().SeedAsync(db);

            Assert.Empty(order.Batches);
            Assert.Equal((12, 5, 4), (await db.CatalogItems.CountAsync(), await db.CatalogBrands.CountAsync(), await db.CatalogTypes.CountAsync()));
        }
    }

    [Fact]
    public async Task Customization_seed_reads_setup_csvs_and_extracts_pictures()
    {
        var setup = TempDirectory();
        var pics = Path.Combine(TempDirectory(), "Pics");
        Directory.CreateDirectory(pics);
        File.WriteAllText(Path.Combine(pics, "stale.png"), "old");
        File.WriteAllLines(Path.Combine(setup, "CatalogTypes.csv"), new[] { "CatalogType", "Fragrance", "Skincare" });
        File.WriteAllLines(Path.Combine(setup, "CatalogBrands.csv"), new[] { "CatalogBrand", "MECCA MAX", "Rationale " });
        File.WriteAllLines(Path.Combine(setup, "CatalogItems.csv"), new[]
        {
            "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName,availablestock",
            "Skincare,Rationale,\"Serum, 30ml\",Serum,189.00,serum.png,7",
            "Fragrance,MECCA MAX,Mist,Mist,25.5,mist.png,",
        });
        using (var zip = ZipFile.Open(Path.Combine(setup, "CatalogItems.zip"), ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("serum.png").Open());
            writer.Write("png");
        }

        await using var db = InMemoryContext();
        await Seeder(useCustomizationData: true, setupPath: setup, picturesPath: pics).SeedAsync(db);

        Assert.Equal(new[] { "Fragrance", "Skincare" }, await db.CatalogTypes.OrderBy(t => t.Id).Select(t => t.Type).ToListAsync());
        Assert.Equal(new[] { "MECCA MAX", "Rationale" }, await db.CatalogBrands.OrderBy(b => b.Id).Select(b => b.Brand).ToListAsync());
        var items = await db.CatalogItems.OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(new[] { (1, "Serum", "Serum, 30ml", 189.00M, 2, 2, 7), (2, "Mist", "Mist", 25.5M, 1, 1, 0) },
            items.Select(i => (i.Id, i.Name, i.Description!, i.Price, i.CatalogTypeId, i.CatalogBrandId, i.AvailableStock)));
        Assert.Equal(new[] { "serum.png" }, Directory.GetFiles(pics).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Customization_without_csv_files_falls_back_to_preconfigured_data_like_legacy()
    {
        var setup = TempDirectory();
        using (ZipFile.Open(Path.Combine(setup, "CatalogItems.zip"), ZipArchiveMode.Create))
        {
        }

        await using var db = InMemoryContext();
        await Seeder(useCustomizationData: true, setupPath: setup, picturesPath: Path.Combine(setup, "Pics")).SeedAsync(db);

        Assert.Equal((12, 5, 4), (await db.CatalogItems.CountAsync(), await db.CatalogBrands.CountAsync(), await db.CatalogTypes.CountAsync()));
        Assert.True(Directory.Exists(Path.Combine(setup, "Pics")));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("bad-entry")]
    public async Task Missing_or_corrupt_zip_fails_without_deleting_existing_pictures(string zipState)
    {
        var setup = TempDirectory();
        var pics = TempDirectory();
        File.WriteAllText(Path.Combine(pics, "1.png"), "keep");
        var zipPath = Path.Combine(setup, "CatalogItems.zip");
        if (zipState == "corrupt")
        {
            File.WriteAllText(zipPath, "not a zip");
        }
        else if (zipState == "bad-entry")
        {
            // A readable archive whose second entry fails during extraction.
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(zip.CreateEntry("2.png").Open()))
            {
                writer.Write("new");
            }

            zip.CreateEntry("../escape.png");
        }

        await using var db = InMemoryContext();
        var seeder = Seeder(useCustomizationData: true, setupPath: setup, picturesPath: pics);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => seeder.SeedAsync(db));
        Assert.True(ex is IOException or InvalidDataException, ex.GetType().FullName);
        Assert.True(File.Exists(Path.Combine(pics, "1.png")));
        Assert.False(File.Exists(Path.Combine(pics, "2.png")));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(pics)!, $".{Path.GetFileName(pics)}.staging-*"));
    }

    [Fact]
    public async Task Without_customization_pictures_folder_is_untouched()
    {
        var pics = TempDirectory();
        File.WriteAllText(Path.Combine(pics, "1.png"), "keep");

        await using var db = InMemoryContext();
        await Seeder(picturesPath: pics).SeedAsync(db);

        Assert.True(File.Exists(Path.Combine(pics, "1.png")));
    }
}
