using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eShopRazorPages.Models.Infrastructure;

/// <summary>
/// Port of the legacy EF6 <c>CreateDatabaseIfNotExists</c> initializer: creates the schema only when the
/// database does not exist yet and seeds it from <see cref="PreconfiguredData"/> or, when
/// <c>UseCustomizationData</c> is enabled, from the CSV/zip files in the <c>Setup</c> folder.
/// </summary>
public class CatalogDBInitializer
{
    private static readonly Regex CsvColumnSplitter = new(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled);

    private readonly CatalogDBContext _context;
    private readonly CatalogSettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CatalogDBInitializer> _logger;

    public CatalogDBInitializer(
        CatalogDBContext context,
        IOptions<CatalogSettings> settings,
        IWebHostEnvironment environment,
        ILogger<CatalogDBInitializer> logger)
    {
        _context = context;
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    public void Initialize()
    {
        if (!_context.Database.EnsureCreated())
        {
            _logger.LogInformation("Catalog database already exists; skipping seed.");
            return;
        }

        _logger.LogInformation("Catalog database created; seeding (UseCustomizationData={UseCustomizationData}).", _settings.UseCustomizationData);

        var typeIdMap = AddCatalogTypes();
        var brandIdMap = AddCatalogBrands();
        AddCatalogItems(typeIdMap, brandIdMap);
        AddCatalogItemPictures();
    }

    private Dictionary<int, CatalogType> AddCatalogTypes()
    {
        var types = _settings.UseCustomizationData
            ? GetCatalogTypesFromFile()
            : PreconfiguredData.GetPreconfiguredCatalogTypes();

        return AddWithGeneratedIds(types, t => t.Id, t => t.Id = 0, _context.CatalogTypes);
    }

    private Dictionary<int, CatalogBrand> AddCatalogBrands()
    {
        var brands = _settings.UseCustomizationData
            ? GetCatalogBrandsFromFile()
            : PreconfiguredData.GetPreconfiguredCatalogBrands();

        return AddWithGeneratedIds(brands, b => b.Id, b => b.Id = 0, _context.CatalogBrands);
    }

    private Dictionary<int, TEntity> AddWithGeneratedIds<TEntity>(
        IEnumerable<TEntity> entities,
        Func<TEntity, int> getId,
        Action<TEntity> clearId,
        DbSet<TEntity> set)
        where TEntity : class
    {
        var originalIds = new Dictionary<int, TEntity>();
        foreach (var entity in entities)
        {
            var originalId = getId(entity);
            if (originalId != 0)
            {
                originalIds[originalId] = entity;
            }

            clearId(entity);
            set.Add(entity);
        }

        _context.SaveChanges();
        return originalIds;
    }

    private void AddCatalogItems(Dictionary<int, CatalogType> typeIdMap, Dictionary<int, CatalogBrand> brandIdMap)
    {
        IEnumerable<CatalogItem> items;
        if (_settings.UseCustomizationData && File.Exists(SetupFile("CatalogItems.csv")))
        {
            items = GetCatalogItemsFromFile();
        }
        else
        {
            items = PreconfiguredData.GetPreconfiguredCatalogItems();
            foreach (var item in items)
            {
                item.CatalogTypeId = typeIdMap.TryGetValue(item.CatalogTypeId, out var type) ? type.Id : item.CatalogTypeId;
                item.CatalogBrandId = brandIdMap.TryGetValue(item.CatalogBrandId, out var brand) ? brand.Id : item.CatalogBrandId;
            }
        }

        foreach (var item in items)
        {
            item.Id = 0;
            _context.CatalogItems.Add(item);
        }

        _context.SaveChanges();
    }

    private void AddCatalogItemPictures()
    {
        if (!_settings.UseCustomizationData)
        {
            return;
        }

        var zipFile = SetupFile("CatalogItems.zip");
        if (!File.Exists(zipFile))
        {
            return;
        }

        var picturePath = new DirectoryInfo(Path.Combine(_environment.WebRootPath, "Pics"));
        picturePath.Create();
        foreach (var file in picturePath.GetFiles())
        {
            file.Delete();
        }

        ZipFile.ExtractToDirectory(zipFile, picturePath.FullName, overwriteFiles: true);
    }

    private string SetupFile(string fileName) => Path.Combine(_environment.ContentRootPath, "Setup", fileName);

    private List<CatalogType> GetCatalogTypesFromFile()
    {
        var csvFile = SetupFile("CatalogTypes.csv");
        if (!File.Exists(csvFile))
        {
            return PreconfiguredData.GetPreconfiguredCatalogTypes();
        }

        GetHeaders(csvFile, new[] { "catalogtype" });

        return File.ReadAllLines(csvFile)
            .Skip(1)
            .Select(row => new CatalogType { Type = RequireValue(row, "catalog Type Name is empty") })
            .ToList();
    }

    private List<CatalogBrand> GetCatalogBrandsFromFile()
    {
        var csvFile = SetupFile("CatalogBrands.csv");
        if (!File.Exists(csvFile))
        {
            return PreconfiguredData.GetPreconfiguredCatalogBrands();
        }

        GetHeaders(csvFile, new[] { "catalogbrand" });

        return File.ReadAllLines(csvFile)
            .Skip(1)
            .Select(row => new CatalogBrand { Brand = RequireValue(row, "catalog Brand Name is empty") })
            .ToList();
    }

    private List<CatalogItem> GetCatalogItemsFromFile()
    {
        var csvFile = SetupFile("CatalogItems.csv");
        string[] requiredHeaders = { "catalogtypename", "catalogbrandname", "description", "name", "price", "picturefilename" };
        string[] optionalHeaders = { "availablestock", "restockthreshold", "maxstockthreshold", "onreorder" };
        var headers = GetHeaders(csvFile, requiredHeaders, optionalHeaders);

        var typeLookup = _context.CatalogTypes.ToDictionary(ct => ct.Type, ct => ct.Id);
        var brandLookup = _context.CatalogBrands.ToDictionary(cb => cb.Brand, cb => cb.Id);

        return File.ReadAllLines(csvFile)
            .Skip(1)
            .Select(row => CsvColumnSplitter.Split(row))
            .Select(columns => CreateCatalogItem(columns, headers, typeLookup, brandLookup))
            .ToList();
    }

    private static string RequireValue(string raw, string errorMessage)
    {
        var value = Clean(raw);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value;
    }

    private static string Clean(string value) => value.Trim('"').Trim();

    private static CatalogItem CreateCatalogItem(
        string[] columns,
        string[] headers,
        Dictionary<string, int> typeLookup,
        Dictionary<string, int> brandLookup)
    {
        if (columns.Length != headers.Length)
        {
            throw new InvalidOperationException($"column count '{columns.Length}' not the same as headers count '{headers.Length}'");
        }

        string Column(string header) => Clean(columns[Array.IndexOf(headers, header)]);

        var typeName = Column("catalogtypename");
        if (!typeLookup.TryGetValue(typeName, out var typeId))
        {
            throw new InvalidOperationException($"type={typeName} does not exist in catalogTypes");
        }

        var brandName = Column("catalogbrandname");
        if (!brandLookup.TryGetValue(brandName, out var brandId))
        {
            throw new InvalidOperationException($"brand={brandName} does not exist in catalogBrands");
        }

        var priceString = Column("price");
        if (!decimal.TryParse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
        {
            throw new InvalidOperationException($"price={priceString} is not a valid decimal number");
        }

        var item = new CatalogItem
        {
            CatalogTypeId = typeId,
            CatalogBrandId = brandId,
            Description = Column("description"),
            Name = Column("name"),
            Price = price,
            PictureFileName = Column("picturefilename"),
        };

        item.AvailableStock = OptionalInt(columns, headers, "availablestock") ?? item.AvailableStock;
        item.RestockThreshold = OptionalInt(columns, headers, "restockthreshold") ?? item.RestockThreshold;
        item.MaxStockThreshold = OptionalInt(columns, headers, "maxstockthreshold") ?? item.MaxStockThreshold;

        var onReorderIndex = Array.IndexOf(headers, "onreorder");
        if (onReorderIndex != -1)
        {
            var onReorderString = Clean(columns[onReorderIndex]);
            if (!string.IsNullOrEmpty(onReorderString))
            {
                if (!bool.TryParse(onReorderString, out var onReorder))
                {
                    throw new InvalidOperationException($"onReorder={onReorderString} is not a valid boolean");
                }

                item.OnReorder = onReorder;
            }
        }

        return item;
    }

    private static int? OptionalInt(string[] columns, string[] headers, string header)
    {
        var index = Array.IndexOf(headers, header);
        if (index == -1)
        {
            return null;
        }

        var raw = Clean(columns[index]);
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"{header}={raw} is not a valid integer");
        }

        return value;
    }

    private static string[] GetHeaders(string csvFile, string[] requiredHeaders, string[]? optionalHeaders = null)
    {
        var headers = File.ReadLines(csvFile).First().ToLowerInvariant().Split(',').Select(h => h.Trim()).ToArray();

        if (headers.Length < requiredHeaders.Length)
        {
            throw new InvalidOperationException($"requiredHeader count '{requiredHeaders.Length}' is bigger than csv header count '{headers.Length}'");
        }

        if (optionalHeaders != null && headers.Length > requiredHeaders.Length + optionalHeaders.Length)
        {
            throw new InvalidOperationException($"csv header count '{headers.Length}' is larger than required '{requiredHeaders.Length}' and optional '{optionalHeaders.Length}' headers count");
        }

        foreach (var requiredHeader in requiredHeaders)
        {
            if (!headers.Contains(requiredHeader.ToLowerInvariant()))
            {
                throw new InvalidOperationException($"does not contain required header '{requiredHeader}'");
            }
        }

        return headers;
    }
}
