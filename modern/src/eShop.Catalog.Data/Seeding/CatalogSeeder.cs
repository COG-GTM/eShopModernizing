using System.IO.Compression;
using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.Data.Seeding;

/// <summary>
/// Idempotent port of the legacy CatalogDBInitializer.Seed: types, then brands, then items (each table only when empty).
/// Items are inserted with Id = 0 so the catalog_hilo HiLo generator assigns 1..12 exactly like legacy.
/// </summary>
public class CatalogSeeder
{
    private readonly CatalogDataOptions options;
    private readonly string setupPath;
    private readonly string picturesPath;
    private readonly ILogger<CatalogSeeder> logger;

    public CatalogSeeder(
        CatalogDataOptions options,
        IOptions<CatalogSeedingOptions> seedingOptions,
        IHostEnvironment? environment = null,
        ILogger<CatalogSeeder>? logger = null)
    {
        this.options = options;
        var contentRoot = environment?.ContentRootPath ?? AppContext.BaseDirectory;
        setupPath = seedingOptions.Value.SetupPath ?? Path.Combine(contentRoot, "Setup");
        picturesPath = seedingOptions.Value.PicturesPath ?? Path.Combine(contentRoot, "Pics");
        this.logger = logger ?? NullLogger<CatalogSeeder>.Instance;
    }

    public async Task SeedAsync(CatalogDbContext db, CancellationToken cancellationToken = default)
    {
        var seededTypes = await SeedCatalogTypesAsync(db, cancellationToken);
        var seededBrands = await SeedCatalogBrandsAsync(db, cancellationToken);
        var seededItems = await SeedCatalogItemsAsync(db, cancellationToken);

        if (seededItems)
        {
            AddCatalogItemPictures();
        }

        logger.LogInformation(
            "Catalog seed complete (types seeded: {Types}, brands seeded: {Brands}, items seeded: {Items}, customization: {Customization})",
            seededTypes,
            seededBrands,
            seededItems,
            options.UseCustomizationData);
    }

    private async Task<bool> SeedCatalogTypesAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (await db.CatalogTypes.AnyAsync(cancellationToken))
        {
            return false;
        }

        var types = options.UseCustomizationData
            ? ReadCsv("CatalogTypes.csv", CatalogCsvParser.ParseCatalogTypes) ?? PreconfiguredData.GetPreconfiguredCatalogTypes()
            : PreconfiguredData.GetPreconfiguredCatalogTypes();

        await AdvanceSequenceAsync(db, CatalogDbContext.CatalogTypeHiLoSequenceName, cancellationToken);
        foreach (var type in types)
        {
            type.Id = 0;
            db.CatalogTypes.Add(type);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> SeedCatalogBrandsAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (await db.CatalogBrands.AnyAsync(cancellationToken))
        {
            return false;
        }

        var brands = options.UseCustomizationData
            ? ReadCsv("CatalogBrands.csv", CatalogCsvParser.ParseCatalogBrands) ?? PreconfiguredData.GetPreconfiguredCatalogBrands()
            : PreconfiguredData.GetPreconfiguredCatalogBrands();

        await AdvanceSequenceAsync(db, CatalogDbContext.CatalogBrandHiLoSequenceName, cancellationToken);
        foreach (var brand in brands)
        {
            brand.Id = 0;
            db.CatalogBrands.Add(brand);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> SeedCatalogItemsAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (await db.CatalogItems.AnyAsync(cancellationToken))
        {
            return false;
        }

        IEnumerable<CatalogItem> items = PreconfiguredData.GetPreconfiguredCatalogItems();
        if (options.UseCustomizationData)
        {
            var typeIds = await db.CatalogTypes.ToDictionaryAsync(ct => ct.Type, ct => ct.Id, cancellationToken);
            var brandIds = await db.CatalogBrands.ToDictionaryAsync(cb => cb.Brand, cb => cb.Id, cancellationToken);
            items = ReadCsv("CatalogItems.csv", lines => CatalogCsvParser.ParseCatalogItems(lines, typeIds, brandIds)) ?? items;
        }

        foreach (var item in items)
        {
            item.Id = 0;
            item.CatalogBrand = null;
            item.CatalogType = null;
            db.CatalogItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IEnumerable<T>? ReadCsv<T>(string fileName, Func<IReadOnlyList<string>, IEnumerable<T>> parse)
    {
        var file = Path.Combine(setupPath, fileName);
        if (!File.Exists(file))
        {
            logger.LogInformation("{File} not found; using preconfigured seed data", file);
            return null;
        }

        return parse(File.ReadAllLines(file));
    }

    // Legacy consumed one NEXT VALUE of catalog_type_hilo / catalog_brand_hilo per seed; ids come from the IDENTITY column.
    private static async Task AdvanceSequenceAsync(CatalogDbContext db, string sequenceName, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer())
        {
            return;
        }

        var sql = sequenceName switch
        {
            CatalogDbContext.CatalogTypeHiLoSequenceName => "SELECT NEXT VALUE FOR [dbo].[catalog_type_hilo] AS [Value]",
            CatalogDbContext.CatalogBrandHiLoSequenceName => "SELECT NEXT VALUE FOR [dbo].[catalog_brand_hilo] AS [Value]",
            _ => throw new ArgumentOutOfRangeException(nameof(sequenceName), sequenceName, "Unknown catalog sequence"),
        };
        await db.Database.SqlQueryRaw<long>(sql).ToListAsync(cancellationToken);
    }

    private void AddCatalogItemPictures()
    {
        if (!options.UseCustomizationData)
        {
            return;
        }

        // Extract into a sibling staging folder first so a missing or corrupt zip fails without deleting existing pictures.
        var pictures = Directory.CreateDirectory(picturesPath);
        var staging = new DirectoryInfo(Path.Combine(pictures.Parent!.FullName, $".{pictures.Name}.staging-{Guid.NewGuid():N}"));
        try
        {
            staging.Create();
            ZipFile.ExtractToDirectory(Path.Combine(setupPath, "CatalogItems.zip"), staging.FullName);

            foreach (var file in pictures.GetFiles())
            {
                file.Delete();
            }

            foreach (var entry in staging.EnumerateFileSystemInfos())
            {
                var target = Path.Combine(pictures.FullName, entry.Name);
                if (entry is DirectoryInfo dir)
                {
                    dir.MoveTo(target);
                }
                else
                {
                    ((FileInfo)entry).MoveTo(target, overwrite: true);
                }
            }
        }
        finally
        {
            if (staging.Exists)
            {
                staging.Delete(recursive: true);
            }
        }
    }
}
