using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using eShop.Catalog.Core.Infrastructure;
using eShop.Catalog.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eShop.Catalog.Infrastructure.Data
{
    // Port of eShopLegacyMVC.Models.Infrastructure.CatalogDBInitializer (EF6 CreateDatabaseIfNotExists):
    // the database, schema and the three HiLo sequences are created only when the database does not exist yet,
    // and only then is the catalog seeded. Restarts against an existing database change nothing.
    public class CatalogDBInitializer
    {
        private const string InitializationLockResource = "eShop.Catalog.CatalogDBInitializer";
        private const int ConnectAttempts = 10;
        private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(3);

        private readonly CatalogDBContext context;
        private readonly CatalogItemHiLoGenerator indexGenerator;
        private readonly ILogger<CatalogDBInitializer> logger;
        private readonly bool useCustomizationData;
        private readonly string contentRootPath;
        private readonly string picturesPath;

        public CatalogDBInitializer(
            CatalogDBContext context,
            CatalogItemHiLoGenerator indexGenerator,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            ILogger<CatalogDBInitializer> logger,
            IWebHostEnvironment webHostEnvironment = null)
        {
            this.context = context;
            this.indexGenerator = indexGenerator;
            this.logger = logger;
            useCustomizationData = configuration.GetCatalogSettings().UseCustomizationData;
            contentRootPath = hostEnvironment.ContentRootPath;
            // Legacy served ~/Pics from the application root; the migrated apps serve it from wwwroot/Pics.
            picturesPath = Path.Combine(webHostEnvironment?.WebRootPath ?? contentRootPath, "Pics");
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Both web apps may start against the same server at once (docker compose): serialise creation with a
            // server-wide application lock so only the first one creates and seeds the database.
            await using var lockConnection = await OpenMasterConnectionAsync(cancellationToken);
            await AcquireInitializationLockAsync(lockConnection, cancellationToken);
            try
            {
                if (!await context.Database.EnsureCreatedAsync(cancellationToken))
                {
                    logger.LogInformation("Catalog database already exists; skipping creation and seeding.");
                    return;
                }

                logger.LogInformation("Created catalog database, schema and HiLo sequences; seeding ({Source}).",
                    useCustomizationData ? "Setup/*.csv" : "PreconfiguredData");
                try
                {
                    Seed();
                }
                catch
                {
                    // Seeding only runs on a freshly created database; drop it so the next start retries from scratch.
                    logger.LogError("Seeding the catalog database failed; dropping it so the next start can retry.");
                    await context.Database.EnsureDeletedAsync(CancellationToken.None);
                    throw;
                }
            }
            finally
            {
                await ReleaseInitializationLockAsync(lockConnection);
            }
        }

        private void Seed()
        {
            AddCatalogTypes();
            AddCatalogBrands();
            AddCatalogItems();
            AddCatalogItemPictures();
        }

        private void AddCatalogTypes()
        {
            var preconfiguredTypes = useCustomizationData
                ? GetCatalogTypesFromFile()
                : PreconfiguredData.GetPreconfiguredCatalogTypes();

            // Legacy draws a value from catalog_type_hilo and assigns it, but CatalogType.Id is an IDENTITY column
            // (EF6 key convention) so EF6 never sent it and SQL Server numbered the rows 1..n. EF Core would send
            // an explicit value and fail, so the sequence is still consumed and the identity assigns the id.
            CatalogItemHiLoGenerator.NextSequenceValue(context, CatalogDBContext.CatalogTypeHiLoSequenceName);
            foreach (var type in preconfiguredTypes)
            {
                type.Id = 0;
                context.CatalogTypes.Add(type);
                context.SaveChanges();
            }
        }

        private void AddCatalogBrands()
        {
            var preconfiguredBrands = useCustomizationData
                ? GetCatalogBrandsFromFile()
                : PreconfiguredData.GetPreconfiguredCatalogBrands();

            CatalogItemHiLoGenerator.NextSequenceValue(context, CatalogDBContext.CatalogBrandHiLoSequenceName);
            foreach (var brand in preconfiguredBrands)
            {
                brand.Id = 0;
                context.CatalogBrands.Add(brand);
                context.SaveChanges();
            }
        }

        private void AddCatalogItems()
        {
            var preconfiguredItems = useCustomizationData
                ? GetCatalogItemsFromFile()
                : PreconfiguredData.GetPreconfiguredCatalogItems();

            foreach (var item in preconfiguredItems)
            {
                var sequenceId = indexGenerator.GetNextSequenceValue(context);
                item.Id = sequenceId;
                context.CatalogItems.Add(item);
            }

            context.SaveChanges();
        }

        private IEnumerable<CatalogType> GetCatalogTypesFromFile()
        {
            string csvFileCatalogTypes = GetSetupFilePath("CatalogTypes.csv");

            if (!File.Exists(csvFileCatalogTypes))
            {
                return PreconfiguredData.GetPreconfiguredCatalogTypes();
            }

            string[] requiredHeaders = { "catalogtype" };
            GetHeaders(csvFileCatalogTypes, requiredHeaders);

            return File.ReadAllLines(csvFileCatalogTypes)
                                        .Skip(1) // skip header row
                                        .Select(x => CreateCatalogType(x))
                                        .Where(x => x != null)
                                        .ToList();
        }

        static CatalogType CreateCatalogType(string type)
        {
            type = type.Trim('"').Trim();

            if (String.IsNullOrEmpty(type))
            {
                throw new Exception("catalog Type Name is empty");
            }

            return new CatalogType
            {
                Type = type,
            };
        }

        private IEnumerable<CatalogBrand> GetCatalogBrandsFromFile()
        {
            string csvFileCatalogBrands = GetSetupFilePath("CatalogBrands.csv");

            if (!File.Exists(csvFileCatalogBrands))
            {
                return PreconfiguredData.GetPreconfiguredCatalogBrands();
            }

            string[] requiredHeaders = { "catalogbrand" };
            GetHeaders(csvFileCatalogBrands, requiredHeaders);

            return File.ReadAllLines(csvFileCatalogBrands)
                                        .Skip(1) // skip header row
                                        .Select(x => CreateCatalogBrand(x))
                                        .Where(x => x != null)
                                        .ToList();
        }

        static CatalogBrand CreateCatalogBrand(string brand)
        {
            brand = brand.Trim('"').Trim();

            if (String.IsNullOrEmpty(brand))
            {
                throw new Exception("catalog Brand Name is empty");
            }

            return new CatalogBrand
            {
                Brand = brand,
            };
        }

        private IEnumerable<CatalogItem> GetCatalogItemsFromFile()
        {
            string csvFileCatalogItems = GetSetupFilePath("CatalogItems.csv");

            if (!File.Exists(csvFileCatalogItems))
            {
                return PreconfiguredData.GetPreconfiguredCatalogItems();
            }

            string[] requiredHeaders = { "catalogtypename", "catalogbrandname", "description", "name", "price", "pictureFileName" };
            string[] optionalheaders = { "availablestock", "restockthreshold", "maxstockthreshold", "onreorder" };
            string[] csvheaders = GetHeaders(csvFileCatalogItems, requiredHeaders, optionalheaders);

            var catalogTypeIdLookup = context.CatalogTypes.ToDictionary(ct => ct.Type, ct => ct.Id);
            var catalogBrandIdLookup = context.CatalogBrands.ToDictionary(ct => ct.Brand, ct => ct.Id);

            return File.ReadAllLines(csvFileCatalogItems)
                        .Skip(1) // skip header row
                        .Select(row => Regex.Split(row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
                        .Select(column => CreateCatalogItem(column, csvheaders, catalogTypeIdLookup, catalogBrandIdLookup))
                        .Where(x => x != null)
                        .ToList();
        }

        static CatalogItem CreateCatalogItem(string[] column, string[] headers, Dictionary<String, int> catalogTypeIdLookup, Dictionary<String, int> catalogBrandIdLookup)
        {
            if (column.Length != headers.Length)
            {
                throw new Exception($"column count '{column.Length}' not the same as headers count'{headers.Length}'");
            }

            string catalogTypeName = column[Array.IndexOf(headers, "catalogtypename")].Trim('"').Trim();
            if (!catalogTypeIdLookup.ContainsKey(catalogTypeName))
            {
                throw new Exception($"type={catalogTypeName} does not exist in catalogTypes");
            }

            string catalogBrandName = column[Array.IndexOf(headers, "catalogbrandname")].Trim('"').Trim();
            if (!catalogBrandIdLookup.ContainsKey(catalogBrandName))
            {
                throw new Exception($"type={catalogTypeName} does not exist in catalogTypes");
            }

            string priceString = column[Array.IndexOf(headers, "price")].Trim('"').Trim();
            if (!Decimal.TryParse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out Decimal price))
            {
                throw new Exception($"price={priceString}is not a valid decimal number");
            }

            var catalogItem = new CatalogItem()
            {
                CatalogTypeId = catalogTypeIdLookup[catalogTypeName],
                CatalogBrandId = catalogBrandIdLookup[catalogBrandName],
                Description = column[Array.IndexOf(headers, "description")].Trim('"').Trim(),
                Name = column[Array.IndexOf(headers, "name")].Trim('"').Trim(),
                Price = price,
                PictureFileName = column[Array.IndexOf(headers, "picturefilename")].Trim('"').Trim(),
            };

            int availableStockIndex = Array.IndexOf(headers, "availablestock");
            if (availableStockIndex != -1)
            {
                string availableStockString = column[availableStockIndex].Trim('"').Trim();
                if (!String.IsNullOrEmpty(availableStockString))
                {
                    if (int.TryParse(availableStockString, out int availableStock))
                    {
                        catalogItem.AvailableStock = availableStock;
                    }
                    else
                    {
                        throw new Exception($"availableStock={availableStockString} is not a valid integer");
                    }
                }
            }

            int restockThresholdIndex = Array.IndexOf(headers, "restockthreshold");
            if (restockThresholdIndex != -1)
            {
                string restockThresholdString = column[restockThresholdIndex].Trim('"').Trim();
                if (!String.IsNullOrEmpty(restockThresholdString))
                {
                    if (int.TryParse(restockThresholdString, out int restockThreshold))
                    {
                        catalogItem.RestockThreshold = restockThreshold;
                    }
                    else
                    {
                        throw new Exception($"restockThreshold={restockThresholdString} is not a valid integer");
                    }
                }
            }

            int maxStockThresholdIndex = Array.IndexOf(headers, "maxstockthreshold");
            if (maxStockThresholdIndex != -1)
            {
                string maxStockThresholdString = column[maxStockThresholdIndex].Trim('"').Trim();
                if (!String.IsNullOrEmpty(maxStockThresholdString))
                {
                    if (int.TryParse(maxStockThresholdString, out int maxStockThreshold))
                    {
                        catalogItem.MaxStockThreshold = maxStockThreshold;
                    }
                    else
                    {
                        throw new Exception($"maxStockThreshold={maxStockThresholdString} is not a valid integer");
                    }
                }
            }

            int onReorderIndex = Array.IndexOf(headers, "onreorder");
            if (onReorderIndex != -1)
            {
                string onReorderString = column[onReorderIndex].Trim('"').Trim();
                if (!String.IsNullOrEmpty(onReorderString))
                {
                    if (bool.TryParse(onReorderString, out bool onReorder))
                    {
                        catalogItem.OnReorder = onReorder;
                    }
                    else
                    {
                        throw new Exception($"onReorder={onReorderString} is not a valid boolean");
                    }
                }
            }

            return catalogItem;
        }

        static string[] GetHeaders(string csvfile, string[] requiredHeaders, string[] optionalHeaders = null)
        {
            string[] csvheaders = File.ReadLines(csvfile).First().ToLowerInvariant().Split(',');

            if (csvheaders.Length < requiredHeaders.Length)
            {
                throw new Exception($"requiredHeader count '{requiredHeaders.Length}' is bigger then csv header count '{csvheaders.Length}' ");
            }

            if (optionalHeaders != null)
            {
                if (csvheaders.Length > (requiredHeaders.Length + optionalHeaders.Length))
                {
                    throw new Exception($"csv header count '{csvheaders.Length}'  is larger then required '{requiredHeaders.Length}' and optional '{optionalHeaders.Length}' headers count");
                }
            }

            foreach (var requiredHeader in requiredHeaders)
            {
                if (!csvheaders.Contains(requiredHeader.ToLowerInvariant()))
                {
                    throw new Exception($"does not contain required header '{requiredHeader}'");
                }
            }

            return csvheaders;
        }

        private void AddCatalogItemPictures()
        {
            if (!useCustomizationData)
            {
                return;
            }

            string zipFileCatalogItemPictures = GetSetupFilePath("CatalogItems.zip");
            if (!File.Exists(zipFileCatalogItemPictures))
            {
                // Legacy threw here after wiping Pics; keep the shipped pictures instead of leaving the folder empty.
                logger.LogWarning("UseCustomizationData=true but {ZipFile} was not found; keeping the existing pictures in {PicturesPath}.",
                    zipFileCatalogItemPictures, picturesPath);
                return;
            }

            DirectoryInfo picturePath = Directory.CreateDirectory(picturesPath);
            foreach (FileInfo file in picturePath.GetFiles())
            {
                file.Delete();
            }

            ZipFile.ExtractToDirectory(zipFileCatalogItemPictures, picturePath.FullName, overwriteFiles: true);
        }

        // Legacy read {app root}/Setup. The seed files ship in this project under Data/Setup; the first existing
        // location wins: content root (legacy layout), then the output directory (Setup/ or Data/Setup/).
        private string GetSetupFilePath(string fileName)
        {
            string[] candidates =
            {
                Path.Combine(contentRootPath, "Setup", fileName),
                Path.Combine(AppContext.BaseDirectory, "Setup", fileName),
                Path.Combine(AppContext.BaseDirectory, "Data", "Setup", fileName),
            };
            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private async Task<SqlConnection> OpenMasterConnectionAsync(CancellationToken cancellationToken)
        {
            var builder = new SqlConnectionStringBuilder(context.Database.GetConnectionString())
            {
                InitialCatalog = "master",
                // A pooled connection keeps its session (and the session-owned app lock) alive after Dispose.
                Pooling = false,
            };

            for (int attempt = 1; ; attempt++)
            {
                var connection = new SqlConnection(builder.ConnectionString);
                try
                {
                    await connection.OpenAsync(cancellationToken);
                    return connection;
                }
                catch (SqlException ex) when (attempt < ConnectAttempts)
                {
                    await connection.DisposeAsync();
                    logger.LogWarning("SQL Server not reachable yet (attempt {Attempt}/{Max}): {Message}", attempt, ConnectAttempts, ex.Message);
                    await Task.Delay(ConnectRetryDelay, cancellationToken);
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            }
        }

        // Session-owned lock: released when the connection is disposed at the end of InitializeAsync.
        private static async Task ReleaseInitializationLockAsync(SqlConnection connection)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
            command.Parameters.AddWithValue("@resource", InitializationLockResource);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task AcquireInitializationLockAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 120000; SELECT @r;";
            command.CommandTimeout = 150;
            command.Parameters.AddWithValue("@resource", InitializationLockResource);
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
            {
                throw new InvalidOperationException($"Could not acquire the catalog initialization lock (sp_getapplock returned {result}).");
            }
        }
    }
}
