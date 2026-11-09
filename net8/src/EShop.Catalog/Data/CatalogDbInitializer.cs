using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using EShop.Catalog.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EShop.Catalog.Data;

/// <summary>
/// Legacy EF6 <c>CatalogDBInitializer : CreateDatabaseIfNotExists</c>: creates the schema, the three HiLo sequences and the seed
/// (PreconfiguredData, or Setup/*.csv + Setup/CatalogItems.zip pictures when <c>UseCustomizationData=true</c>).
/// Runs once at startup under a SQL Server application lock so apps sharing one database never race.
/// </summary>
public partial class CatalogDbInitializer
{
    private const string TypeSequenceName = "catalog_type_hilo";
    private const string BrandSequenceName = "catalog_brand_hilo";
    private static readonly string[] SequenceNames = { CatalogItemHiLoGenerator.SequenceName, BrandSequenceName, TypeSequenceName };
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ServerWaitTimeout = TimeSpan.FromSeconds(90);

    private readonly CatalogDbContext db;
    private readonly CatalogItemHiLoGenerator indexGenerator;
    private readonly CatalogOptions options;
    private readonly IWebHostEnvironment? environment;
    private readonly ILogger<CatalogDbInitializer> logger;

    public CatalogDbInitializer(
        CatalogDbContext db,
        CatalogItemHiLoGenerator indexGenerator,
        IOptions<CatalogOptions> options,
        ILogger<CatalogDbInitializer> logger,
        IWebHostEnvironment? environment = null)
    {
        this.db = db;
        this.indexGenerator = indexGenerator;
        this.options = options.Value;
        this.logger = logger;
        this.environment = environment;
    }

    /// <summary>Directory holding the legacy Setup/ seed files (copied next to the app assemblies).</summary>
    public string SetupDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Setup");

    /// <summary>Directory the customization pictures are extracted to (legacy <c>Pics/</c>; here <c>wwwroot/Pics</c>).</summary>
    public string? PicturesDirectory { get; set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException($"ConnectionStrings:{CatalogOptions.ConnectionStringName} is required when UseMockData=false.");
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        await using var lockConnection = await OpenLockConnectionAsync(connectionString, cancellationToken);
        var resource = $"EShop.Catalog.Init:{database}";
        await AcquireAppLockAsync(lockConnection, resource, cancellationToken);
        try
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            if (await SequenceExistsAsync(CatalogItemHiLoGenerator.SequenceName, cancellationToken))
            {
                logger.LogInformation("Catalog database {Database} already seeded", database);
            }
            else
            {
                await SeedAsync(cancellationToken);
                logger.LogInformation("Catalog database {Database} created and seeded (customization data: {Custom})", database, options.UseCustomizationData);
            }

            // Pictures live in each app's own web root, so an app joining a database another app already seeded still needs them.
            AddCatalogItemPictures();
        }
        finally
        {
            await ReleaseAppLockAsync(lockConnection, resource);
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var sequence in SequenceNames)
        {
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE SEQUENCE [dbo].[{sequence}] AS [bigint] START WITH 1 INCREMENT BY 10 " +
                "MINVALUE -9223372036854775808 MAXVALUE 9223372036854775807 CACHE", cancellationToken);
        }

        AddCatalogTypes();
        AddCatalogBrands();
        AddCatalogItems();
        await transaction.CommitAsync(cancellationToken);
    }

    private void AddCatalogTypes()
    {
        var types = options.UseCustomizationData ? GetCatalogTypesFromFile() : PreconfiguredData.GetPreconfiguredCatalogTypes();
        var sequenceId = (int)CatalogItemHiLoGenerator.NextSequenceValue(db, TypeSequenceName);
        foreach (var type in types)
        {
            type.Id = sequenceId++;
            db.CatalogTypes.Add(type);
        }

        db.SaveChanges();
    }

    private void AddCatalogBrands()
    {
        var brands = options.UseCustomizationData ? GetCatalogBrandsFromFile() : PreconfiguredData.GetPreconfiguredCatalogBrands();
        var sequenceId = (int)CatalogItemHiLoGenerator.NextSequenceValue(db, BrandSequenceName);
        foreach (var brand in brands)
        {
            brand.Id = sequenceId++;
            db.CatalogBrands.Add(brand);
        }

        db.SaveChanges();
    }

    private void AddCatalogItems()
    {
        var items = options.UseCustomizationData ? GetCatalogItemsFromFile() : PreconfiguredData.GetPreconfiguredCatalogItems();
        foreach (var item in items)
        {
            item.Id = indexGenerator.GetNextSequenceValue(db);
            db.CatalogItems.Add(item);
        }

        db.SaveChanges();
    }

    private void AddCatalogItemPictures()
    {
        if (!options.UseCustomizationData)
        {
            return;
        }

        var zipFile = Path.Combine(SetupDirectory, "CatalogItems.zip");
        var picturePath = PicturesDirectory ?? Path.Combine(
            environment?.WebRootPath ?? Path.Combine(environment?.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot"), "Pics");
        if (!File.Exists(zipFile))
        {
            logger.LogWarning("UseCustomizationData=true but {ZipFile} is missing; pictures left unchanged", zipFile);
            return;
        }

        var directory = Directory.CreateDirectory(picturePath);
        foreach (var file in directory.GetFiles())
        {
            file.Delete();
        }

        ZipFile.ExtractToDirectory(zipFile, directory.FullName, overwriteFiles: true);
    }

    private List<CatalogType> GetCatalogTypesFromFile()
    {
        var csvFile = Path.Combine(SetupDirectory, "CatalogTypes.csv");
        if (!File.Exists(csvFile))
        {
            return PreconfiguredData.GetPreconfiguredCatalogTypes();
        }

        GetHeaders(csvFile, new[] { "catalogtype" });
        return File.ReadAllLines(csvFile).Skip(1)
            .Select(line => new CatalogType { Type = RequireValue(line, "catalog Type Name is empty") })
            .ToList();
    }

    private List<CatalogBrand> GetCatalogBrandsFromFile()
    {
        var csvFile = Path.Combine(SetupDirectory, "CatalogBrands.csv");
        if (!File.Exists(csvFile))
        {
            return PreconfiguredData.GetPreconfiguredCatalogBrands();
        }

        GetHeaders(csvFile, new[] { "catalogbrand" });
        return File.ReadAllLines(csvFile).Skip(1)
            .Select(line => new CatalogBrand { Brand = RequireValue(line, "catalog Brand Name is empty") })
            .ToList();
    }

    private List<CatalogItem> GetCatalogItemsFromFile()
    {
        var csvFile = Path.Combine(SetupDirectory, "CatalogItems.csv");
        if (!File.Exists(csvFile))
        {
            return PreconfiguredData.GetPreconfiguredCatalogItems();
        }

        string[] requiredHeaders = { "catalogtypename", "catalogbrandname", "description", "name", "price", "pictureFileName" };
        string[] optionalHeaders = { "availablestock", "restockthreshold", "maxstockthreshold", "onreorder" };
        var headers = GetHeaders(csvFile, requiredHeaders, optionalHeaders);

        var typeIds = db.CatalogTypes.Local.ToDictionary(t => t.Type, t => t.Id);
        var brandIds = db.CatalogBrands.Local.ToDictionary(b => b.Brand, b => b.Id);

        return File.ReadAllLines(csvFile).Skip(1)
            .Select(row => CsvColumnSplitter().Split(row))
            .Select(columns => CreateCatalogItem(columns, headers, typeIds, brandIds))
            .ToList();
    }

    private static CatalogItem CreateCatalogItem(string[] column, string[] headers, Dictionary<string, int> typeIds, Dictionary<string, int> brandIds)
    {
        if (column.Length != headers.Length)
        {
            throw new InvalidDataException($"column count '{column.Length}' not the same as headers count'{headers.Length}'");
        }

        string Value(string header) => column[Array.IndexOf(headers, header)].Trim('"').Trim();

        var typeName = Value("catalogtypename");
        if (!typeIds.TryGetValue(typeName, out var typeId))
        {
            throw new InvalidDataException($"type={typeName} does not exist in catalogTypes");
        }

        var brandName = Value("catalogbrandname");
        if (!brandIds.TryGetValue(brandName, out var brandId))
        {
            throw new InvalidDataException($"brand={brandName} does not exist in catalogBrands");
        }

        var priceString = Value("price");
        if (!decimal.TryParse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
        {
            throw new InvalidDataException($"price={priceString}is not a valid decimal number");
        }

        var item = new CatalogItem
        {
            CatalogTypeId = typeId,
            CatalogBrandId = brandId,
            Description = Value("description"),
            Name = Value("name"),
            Price = price,
            PictureFileName = Value("picturefilename"),
        };

        item.AvailableStock = OptionalInt(column, headers, "availablestock") ?? item.AvailableStock;
        item.RestockThreshold = OptionalInt(column, headers, "restockthreshold") ?? item.RestockThreshold;
        item.MaxStockThreshold = OptionalInt(column, headers, "maxstockthreshold") ?? item.MaxStockThreshold;

        var onReorderIndex = Array.IndexOf(headers, "onreorder");
        if (onReorderIndex != -1)
        {
            var onReorder = column[onReorderIndex].Trim('"').Trim();
            if (!string.IsNullOrEmpty(onReorder))
            {
                item.OnReorder = bool.TryParse(onReorder, out var parsed)
                    ? parsed
                    : throw new InvalidDataException($"onReorder={onReorder} is not a valid boolean");
            }
        }

        return item;
    }

    private static int? OptionalInt(string[] column, string[] headers, string header)
    {
        var index = Array.IndexOf(headers, header);
        if (index == -1)
        {
            return null;
        }

        var value = column[index].Trim('"').Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return int.TryParse(value, out var parsed) ? parsed : throw new InvalidDataException($"{header}={value} is not a valid integer");
    }

    private static string RequireValue(string line, string emptyMessage)
    {
        var value = line.Trim('"').Trim();
        return string.IsNullOrEmpty(value) ? throw new InvalidDataException(emptyMessage) : value;
    }

    private static string[] GetHeaders(string csvFile, string[] requiredHeaders, string[]? optionalHeaders = null)
    {
        var headers = File.ReadLines(csvFile).First().ToLowerInvariant().Split(',');
        if (headers.Length < requiredHeaders.Length)
        {
            throw new InvalidDataException($"requiredHeader count '{requiredHeaders.Length}' is bigger then csv header count '{headers.Length}'");
        }

        if (optionalHeaders != null && headers.Length > requiredHeaders.Length + optionalHeaders.Length)
        {
            throw new InvalidDataException($"csv header count '{headers.Length}' is larger then required '{requiredHeaders.Length}' and optional '{optionalHeaders.Length}' headers count");
        }

        foreach (var required in requiredHeaders)
        {
            if (!headers.Contains(required.ToLowerInvariant()))
            {
                throw new InvalidDataException($"does not contain required header '{required}'");
            }
        }

        return headers;
    }

    private async Task<bool> SequenceExistsAsync(string sequence, CancellationToken cancellationToken) =>
        await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.sequences WHERE name = {sequence} AND schema_id = SCHEMA_ID('dbo')")
            .SingleAsync(cancellationToken) > 0;

    // Lock connection: master database (the catalog may not exist yet), no pooling so the session-owned lock dies with the connection.
    private async Task<SqlConnection> OpenLockConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", Pooling = false };
        var deadline = DateTime.UtcNow + ServerWaitTimeout;
        while (true)
        {
            var connection = new SqlConnection(builder.ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (SqlException ex) when (DateTime.UtcNow < deadline)
            {
                await connection.DisposeAsync();
                logger.LogWarning("SQL Server not reachable yet ({Message}); retrying", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    private static async Task AcquireAppLockAsync(SqlConnection connection, string resource, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.CommandTimeout = (int)LockTimeout.TotalSeconds + 30;
        command.Parameters.AddWithValue("@Resource", resource);
        command.Parameters.AddWithValue("@LockMode", "Exclusive");
        command.Parameters.AddWithValue("@LockOwner", "Session");
        command.Parameters.AddWithValue("@LockTimeout", (int)LockTimeout.TotalMilliseconds);
        var result = command.Parameters.Add("@Result", System.Data.SqlDbType.Int);
        result.Direction = System.Data.ParameterDirection.ReturnValue;
        await command.ExecuteNonQueryAsync(cancellationToken);
        if ((int)result.Value < 0)
        {
            throw new TimeoutException($"sp_getapplock '{resource}' failed with {result.Value}");
        }
    }

    private async Task ReleaseAppLockAsync(SqlConnection connection, string resource)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "sp_releaseapplock";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Resource", resource);
            command.Parameters.AddWithValue("@LockOwner", "Session");
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "sp_releaseapplock '{Resource}' failed; the lock is released when the connection closes", resource);
        }
    }

    [GeneratedRegex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")]
    private static partial Regex CsvColumnSplitter();
}
