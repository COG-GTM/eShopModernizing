using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using eShop.Catalog.Core.Configuration;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.Infrastructure.Data;

// Port of eShopLegacyMVC.Models.Infrastructure.CatalogDBInitializer (EF6 CreateDatabaseIfNotExists):
// create the database if it does not exist, then (only while the catalog is empty) seed types, brands, items and,
// with UseCustomizationData, the CSV data and the CatalogItems.zip pictures. The three HiLo sequences are part of the
// EF Core model, so EnsureCreated creates them with the tables (legacy ran dbo.*.Sequence.sql scripts instead).
public class CatalogDBInitializer
{
    private const string SetupFolder = "Setup";
    private const string PicsFolder = "Pics";
    private const int MaxConnectAttempts = 20;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SeedLockWait = TimeSpan.FromMinutes(2);

    private readonly CatalogDBContext context;
    private readonly ILogger<CatalogDBInitializer> logger;
    private readonly bool useCustomizationData;
    private readonly string contentRootPath;

    public CatalogDBInitializer(CatalogDBContext context, CatalogSettings settings, ILogger<CatalogDBInitializer> logger, IHostEnvironment? environment = null)
    {
        this.context = context;
        this.logger = logger;
        useCustomizationData = settings.UseCustomizationData;
        contentRootPath = environment?.ContentRootPath ?? AppContext.BaseDirectory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var created = await EnsureCreatedAsync(cancellationToken);

        // MVC and WebForms share the database and both initialize it, so the seed runs under an exclusive app lock in
        // one transaction: a failed seed rolls back to empty tables (retried on the next start, never dropped), and a
        // concurrent instance waits for the commit instead of reading or reseeding a half-seeded catalog.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var commandTimeout = context.Database.GetCommandTimeout();
        context.Database.SetCommandTimeout(SeedLockWait + TimeSpan.FromSeconds(30));
        await context.Database.ExecuteSqlRawAsync(
            "DECLARE @result int; " +
            "EXEC @result = sp_getapplock @Resource = N'eShop.Catalog.Seed', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = @lockTimeoutMs; " +
            "IF @result < 0 THROW 50000, 'Timed out waiting for the catalog seed lock', 1;",
            new object[] { new SqlParameter("@lockTimeoutMs", (int)SeedLockWait.TotalMilliseconds) },
            cancellationToken);
        context.Database.SetCommandTimeout(commandTimeout);

        if (await context.CatalogTypes.AnyAsync(cancellationToken)
            || await context.CatalogBrands.AnyAsync(cancellationToken)
            || await context.CatalogItems.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Catalog database already exists; skipping seed");
            return;
        }

        if (!created)
        {
            // Brands and types cannot be deleted through the app, so all-empty tables mean an earlier seed never
            // committed. Sequences are not transactional: restart them so the reseed gets the legacy ids again.
            logger.LogWarning("Catalog database exists but is empty (an earlier seed did not complete); seeding");
            await context.Database.ExecuteSqlRawAsync(
                $"ALTER SEQUENCE [dbo].[{CatalogDBContext.CatalogTypeHiLoSequenceName}] RESTART WITH 1; " +
                $"ALTER SEQUENCE [dbo].[{CatalogDBContext.CatalogBrandHiLoSequenceName}] RESTART WITH 1; " +
                $"ALTER SEQUENCE [dbo].[{CatalogDBContext.CatalogItemHiLoSequenceName}] RESTART WITH 1;",
                cancellationToken);
        }

        logger.LogInformation("Seeding catalog database (UseCustomizationData={UseCustomizationData})", useCustomizationData);
        Seed();
        await transaction.CommitAsync(cancellationToken);
    }

    public void Seed()
    {
        // Pictures first (legacy extracted them last): a broken archive then fails before any row is written.
        AddCatalogItemPictures();
        AddCatalogTypes();
        AddCatalogBrands();
        AddCatalogItems();
    }

    private async Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await context.Database.EnsureCreatedAsync(cancellationToken);
            }
            catch (SqlException ex) when (attempt < MaxConnectAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("SQL Server not reachable yet (attempt {Attempt}/{MaxAttempts}): {Message}", attempt, MaxConnectAttempts, ex.Message);
                await Task.Delay(ConnectRetryDelay, cancellationToken);
            }
        }
    }

    // Ids come from the HiLo sequences (UseHiLo), so seed entities are added with Id = 0. Legacy assigned
    // NEXT VALUE FOR catalog_type_hilo / catalog_brand_hilo + n and catalog_hilo blocks of 10, which yields the same ids.
    private void AddCatalogTypes()
    {
        var types = useCustomizationData
            ? GetCatalogTypesFromFile()
            : PreconfiguredData.GetPreconfiguredCatalogTypes();

        foreach (var type in types)
        {
            type.Id = 0;
            context.CatalogTypes.Add(type);
        }

        context.SaveChanges();
        logger.LogInformation("Seeded {Count} catalog types", context.CatalogTypes.Local.Count);
    }

    private void AddCatalogBrands()
    {
        var brands = useCustomizationData
            ? GetCatalogBrandsFromFile()
            : PreconfiguredData.GetPreconfiguredCatalogBrands();

        foreach (var brand in brands)
        {
            brand.Id = 0;
            context.CatalogBrands.Add(brand);
        }

        context.SaveChanges();
        logger.LogInformation("Seeded {Count} catalog brands", context.CatalogBrands.Local.Count);
    }

    private void AddCatalogItems()
    {
        var items = useCustomizationData
            ? GetCatalogItemsFromFile()
            : PreconfiguredData.GetPreconfiguredCatalogItems();

        foreach (var item in items)
        {
            item.Id = 0;
            context.CatalogItems.Add(item);
        }

        context.SaveChanges();
        logger.LogInformation("Seeded {Count} catalog items", context.CatalogItems.Local.Count);
    }

    private void AddCatalogItemPictures()
    {
        if (!useCustomizationData)
        {
            return;
        }

        var picturePath = new DirectoryInfo(Path.Combine(contentRootPath, PicsFolder));
        picturePath.Create();
        foreach (var file in picturePath.GetFiles())
        {
            file.Delete();
        }

        var zipFileCatalogItemPictures = GetSetupFilePath("CatalogItems.zip");
        if (zipFileCatalogItemPictures is null)
        {
            throw new FileNotFoundException($"{SetupFolder}/CatalogItems.zip not found");
        }

        ZipFile.ExtractToDirectory(zipFileCatalogItemPictures, picturePath.FullName, overwriteFiles: true);
        logger.LogInformation("Extracted catalog item pictures to {PicturePath}", picturePath.FullName);
    }

    // Legacy read Setup/ under the web root; the files now ship with this assembly (copied to the output folder),
    // and a Setup/ folder under the app content root still takes precedence.
    private string? GetSetupFilePath(string fileName)
    {
        foreach (var root in new[] { contentRootPath, AppContext.BaseDirectory })
        {
            var path = Path.Combine(root, SetupFolder, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private IEnumerable<CatalogType> GetCatalogTypesFromFile()
    {
        var csvFileCatalogTypes = GetSetupFilePath("CatalogTypes.csv");
        if (csvFileCatalogTypes is null)
        {
            return PreconfiguredData.GetPreconfiguredCatalogTypes();
        }

        string[] requiredHeaders = { "catalogtype" };
        GetHeaders(csvFileCatalogTypes, requiredHeaders);

        return File.ReadAllLines(csvFileCatalogTypes)
            .Skip(1) // skip header row
            .Select(CreateCatalogType)
            .ToList();
    }

    private static CatalogType CreateCatalogType(string type)
    {
        type = type.Trim('"').Trim();

        if (string.IsNullOrEmpty(type))
        {
            throw new Exception("catalog Type Name is empty");
        }

        return new CatalogType { Type = type };
    }

    private IEnumerable<CatalogBrand> GetCatalogBrandsFromFile()
    {
        var csvFileCatalogBrands = GetSetupFilePath("CatalogBrands.csv");
        if (csvFileCatalogBrands is null)
        {
            return PreconfiguredData.GetPreconfiguredCatalogBrands();
        }

        string[] requiredHeaders = { "catalogbrand" };
        GetHeaders(csvFileCatalogBrands, requiredHeaders);

        return File.ReadAllLines(csvFileCatalogBrands)
            .Skip(1) // skip header row
            .Select(CreateCatalogBrand)
            .ToList();
    }

    private static CatalogBrand CreateCatalogBrand(string brand)
    {
        brand = brand.Trim('"').Trim();

        if (string.IsNullOrEmpty(brand))
        {
            throw new Exception("catalog Brand Name is empty");
        }

        return new CatalogBrand { Brand = brand };
    }

    private IEnumerable<CatalogItem> GetCatalogItemsFromFile()
    {
        var csvFileCatalogItems = GetSetupFilePath("CatalogItems.csv");
        if (csvFileCatalogItems is null)
        {
            return PreconfiguredData.GetPreconfiguredCatalogItems();
        }

        string[] requiredHeaders = { "catalogtypename", "catalogbrandname", "description", "name", "price", "pictureFileName" };
        string[] optionalHeaders = { "availablestock", "restockthreshold", "maxstockthreshold", "onreorder" };
        var csvHeaders = GetHeaders(csvFileCatalogItems, requiredHeaders, optionalHeaders);

        var catalogTypeIdLookup = context.CatalogTypes.ToDictionary(ct => ct.Type, ct => ct.Id);
        var catalogBrandIdLookup = context.CatalogBrands.ToDictionary(cb => cb.Brand, cb => cb.Id);

        return File.ReadAllLines(csvFileCatalogItems)
            .Skip(1) // skip header row
            .Select(row => Regex.Split(row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
            .Select(column => CreateCatalogItem(column, csvHeaders, catalogTypeIdLookup, catalogBrandIdLookup))
            .ToList();
    }

    private static CatalogItem CreateCatalogItem(string[] column, string[] headers, Dictionary<string, int> catalogTypeIdLookup, Dictionary<string, int> catalogBrandIdLookup)
    {
        if (column.Length != headers.Length)
        {
            throw new Exception($"column count '{column.Length}' not the same as headers count'{headers.Length}'");
        }

        var catalogTypeName = column[Array.IndexOf(headers, "catalogtypename")].Trim('"').Trim();
        if (!catalogTypeIdLookup.ContainsKey(catalogTypeName))
        {
            throw new Exception($"type={catalogTypeName} does not exist in catalogTypes");
        }

        var catalogBrandName = column[Array.IndexOf(headers, "catalogbrandname")].Trim('"').Trim();
        if (!catalogBrandIdLookup.ContainsKey(catalogBrandName))
        {
            throw new Exception($"brand={catalogBrandName} does not exist in catalogBrands");
        }

        var priceString = column[Array.IndexOf(headers, "price")].Trim('"').Trim();
        if (!decimal.TryParse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
        {
            throw new Exception($"price={priceString}is not a valid decimal number");
        }

        var catalogItem = new CatalogItem
        {
            CatalogTypeId = catalogTypeIdLookup[catalogTypeName],
            CatalogBrandId = catalogBrandIdLookup[catalogBrandName],
            Description = column[Array.IndexOf(headers, "description")].Trim('"').Trim(),
            Name = column[Array.IndexOf(headers, "name")].Trim('"').Trim(),
            Price = price,
            PictureFileName = column[Array.IndexOf(headers, "picturefilename")].Trim('"').Trim(),
        };

        if (TryGetOptional(column, headers, "availablestock", out var availableStockString))
        {
            catalogItem.AvailableStock = int.TryParse(availableStockString, out var availableStock)
                ? availableStock
                : throw new Exception($"availableStock={availableStockString} is not a valid integer");
        }

        if (TryGetOptional(column, headers, "restockthreshold", out var restockThresholdString))
        {
            catalogItem.RestockThreshold = int.TryParse(restockThresholdString, out var restockThreshold)
                ? restockThreshold
                : throw new Exception($"restockThreshold={restockThresholdString} is not a valid integer");
        }

        if (TryGetOptional(column, headers, "maxstockthreshold", out var maxStockThresholdString))
        {
            catalogItem.MaxStockThreshold = int.TryParse(maxStockThresholdString, out var maxStockThreshold)
                ? maxStockThreshold
                : throw new Exception($"maxStockThreshold={maxStockThresholdString} is not a valid integer");
        }

        if (TryGetOptional(column, headers, "onreorder", out var onReorderString))
        {
            catalogItem.OnReorder = bool.TryParse(onReorderString, out var onReorder)
                ? onReorder
                : throw new Exception($"onReorder={onReorderString} is not a valid boolean");
        }

        return catalogItem;
    }

    private static bool TryGetOptional(string[] column, string[] headers, string header, out string value)
    {
        var index = Array.IndexOf(headers, header);
        value = index == -1 ? string.Empty : column[index].Trim('"').Trim();
        return value.Length > 0;
    }

    private static string[] GetHeaders(string csvFile, string[] requiredHeaders, string[]? optionalHeaders = null)
    {
        var csvHeaders = File.ReadLines(csvFile).First().ToLowerInvariant().Split(',');

        if (csvHeaders.Length < requiredHeaders.Length)
        {
            throw new Exception($"requiredHeader count '{requiredHeaders.Length}' is bigger then csv header count '{csvHeaders.Length}' ");
        }

        if (optionalHeaders != null && csvHeaders.Length > requiredHeaders.Length + optionalHeaders.Length)
        {
            throw new Exception($"csv header count '{csvHeaders.Length}'  is larger then required '{requiredHeaders.Length}' and optional '{optionalHeaders.Length}' headers count");
        }

        foreach (var requiredHeader in requiredHeaders)
        {
            if (!csvHeaders.Contains(requiredHeader.ToLowerInvariant()))
            {
                throw new Exception($"does not contain required header '{requiredHeader}'");
            }
        }

        return csvHeaders;
    }
}
