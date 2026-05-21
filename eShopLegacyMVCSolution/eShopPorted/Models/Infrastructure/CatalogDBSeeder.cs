using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace eShopPorted.Models.Infrastructure
{
    public static class CatalogDBSeeder
    {
        public static void Seed(CatalogDBContext context, CatalogItemHiLoGenerator indexGenerator, string contentRootPath, bool useCustomizationData)
        {
            context.Database.EnsureCreated();

            if (context.CatalogTypes.Any())
            {
                return;
            }

            AddCatalogTypes(context, contentRootPath, useCustomizationData);
            AddCatalogBrands(context, contentRootPath, useCustomizationData);
            AddCatalogItems(context, indexGenerator, contentRootPath, useCustomizationData);
        }

        private static void AddCatalogTypes(CatalogDBContext context, string contentRootPath, bool useCustomizationData)
        {
            var types = useCustomizationData
                ? GetCatalogTypesFromFile(contentRootPath)
                : PreconfiguredData.GetPreconfiguredCatalogTypes();

            foreach (var type in types)
            {
                context.CatalogTypes.Add(type);
            }
            context.SaveChanges();
        }

        private static void AddCatalogBrands(CatalogDBContext context, string contentRootPath, bool useCustomizationData)
        {
            var brands = useCustomizationData
                ? GetCatalogBrandsFromFile(contentRootPath)
                : PreconfiguredData.GetPreconfiguredCatalogBrands();

            foreach (var brand in brands)
            {
                context.CatalogBrands.Add(brand);
            }
            context.SaveChanges();
        }

        private static void AddCatalogItems(CatalogDBContext context, CatalogItemHiLoGenerator indexGenerator, string contentRootPath, bool useCustomizationData)
        {
            var items = useCustomizationData
                ? GetCatalogItemsFromFile(contentRootPath, context)
                : PreconfiguredData.GetPreconfiguredCatalogItems();

            foreach (var item in items)
            {
                context.CatalogItems.Add(item);
            }
            context.SaveChanges();
        }

        private static IEnumerable<CatalogType> GetCatalogTypesFromFile(string contentRootPath)
        {
            string csvFile = Path.Combine(contentRootPath, "Setup", "CatalogTypes.csv");
            if (!File.Exists(csvFile))
                return PreconfiguredData.GetPreconfiguredCatalogTypes();

            return File.ReadAllLines(csvFile)
                .Skip(1)
                .Select(line => line.Trim('"').Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => new CatalogType { Type = line });
        }

        private static IEnumerable<CatalogBrand> GetCatalogBrandsFromFile(string contentRootPath)
        {
            string csvFile = Path.Combine(contentRootPath, "Setup", "CatalogBrands.csv");
            if (!File.Exists(csvFile))
                return PreconfiguredData.GetPreconfiguredCatalogBrands();

            return File.ReadAllLines(csvFile)
                .Skip(1)
                .Select(line => line.Trim('"').Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => new CatalogBrand { Brand = line });
        }

        private static IEnumerable<CatalogItem> GetCatalogItemsFromFile(string contentRootPath, CatalogDBContext context)
        {
            string csvFile = Path.Combine(contentRootPath, "Setup", "CatalogItems.csv");
            if (!File.Exists(csvFile))
                return PreconfiguredData.GetPreconfiguredCatalogItems();

            string[] requiredHeaders = { "catalogtypename", "catalogbrandname", "description", "name", "price", "picturefilename" };
            string[] headers = GetHeaders(csvFile, requiredHeaders);

            var catalogTypeIdLookup = context.CatalogTypes.ToDictionary(ct => ct.Type, ct => ct.Id);
            var catalogBrandIdLookup = context.CatalogBrands.ToDictionary(cb => cb.Brand, cb => cb.Id);

            return File.ReadAllLines(csvFile)
                .Skip(1)
                .Select(row => Regex.Split(row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
                .Select(column => CreateCatalogItem(column, headers, catalogTypeIdLookup, catalogBrandIdLookup))
                .Where(x => x != null);
        }

        private static CatalogItem CreateCatalogItem(string[] column, string[] headers,
            Dictionary<string, int> catalogTypeIdLookup, Dictionary<string, int> catalogBrandIdLookup)
        {
            var catalogItem = new CatalogItem
            {
                CatalogTypeId = catalogTypeIdLookup[column[Array.IndexOf(headers, "catalogtypename")].Trim('"').Trim()],
                CatalogBrandId = catalogBrandIdLookup[column[Array.IndexOf(headers, "catalogbrandname")].Trim('"').Trim()],
                Description = column[Array.IndexOf(headers, "description")].Trim('"').Trim(),
                Name = column[Array.IndexOf(headers, "name")].Trim('"').Trim(),
                Price = decimal.Parse(column[Array.IndexOf(headers, "price")].Trim('"').Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
                PictureFileName = column[Array.IndexOf(headers, "picturefilename")].Trim('"').Trim(),
            };

            int availableStockIndex = Array.IndexOf(headers, "availablestock");
            if (availableStockIndex != -1)
            {
                var availableStockStr = column[availableStockIndex].Trim('"').Trim();
                if (!string.IsNullOrEmpty(availableStockStr) && int.TryParse(availableStockStr, out int availableStock))
                {
                    catalogItem.AvailableStock = availableStock;
                }
            }

            int restockThresholdIndex = Array.IndexOf(headers, "restockthreshold");
            if (restockThresholdIndex != -1)
            {
                var restockThresholdStr = column[restockThresholdIndex].Trim('"').Trim();
                if (!string.IsNullOrEmpty(restockThresholdStr) && int.TryParse(restockThresholdStr, out int restockThreshold))
                {
                    catalogItem.RestockThreshold = restockThreshold;
                }
            }

            int maxStockThresholdIndex = Array.IndexOf(headers, "maxstockthreshold");
            if (maxStockThresholdIndex != -1)
            {
                var maxStockThresholdStr = column[maxStockThresholdIndex].Trim('"').Trim();
                if (!string.IsNullOrEmpty(maxStockThresholdStr) && int.TryParse(maxStockThresholdStr, out int maxStockThreshold))
                {
                    catalogItem.MaxStockThreshold = maxStockThreshold;
                }
            }

            return catalogItem;
        }

        private static string[] GetHeaders(string csvFile, string[] requiredHeaders)
        {
            string[] csvHeaders = File.ReadLines(csvFile).First().ToLowerInvariant().Split(',');
            foreach (var requiredHeader in requiredHeaders)
            {
                if (!csvHeaders.Contains(requiredHeader))
                {
                    throw new Exception($"Required header '{requiredHeader}' not found in CSV file");
                }
            }
            return csvHeaders;
        }
    }
}
