using System.Globalization;
using System.Text.RegularExpressions;
using eShop.Catalog.Data.Models;

namespace eShop.Catalog.Data.Seeding;

/// <summary>
/// Port of the legacy CatalogDBInitializer CSV readers (Setup/CatalogTypes.csv, CatalogBrands.csv, CatalogItems.csv),
/// including header validation, quoted-comma splitting and the legacy exception messages.
/// </summary>
public static class CatalogCsvParser
{
    public static readonly string[] CatalogTypeRequiredHeaders = { "catalogtype" };
    public static readonly string[] CatalogBrandRequiredHeaders = { "catalogbrand" };
    public static readonly string[] CatalogItemRequiredHeaders = { "catalogtypename", "catalogbrandname", "description", "name", "price", "pictureFileName" };
    public static readonly string[] CatalogItemOptionalHeaders = { "availablestock", "restockthreshold", "maxstockthreshold", "onreorder" };

    private static readonly Regex QuotedCommaSplitter = new(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled);

    public static IEnumerable<CatalogType> ParseCatalogTypes(IReadOnlyList<string> lines)
    {
        GetHeaders(lines, CatalogTypeRequiredHeaders);
        return lines.Skip(1).Select(CreateCatalogType).ToList();
    }

    public static IEnumerable<CatalogBrand> ParseCatalogBrands(IReadOnlyList<string> lines)
    {
        GetHeaders(lines, CatalogBrandRequiredHeaders);
        return lines.Skip(1).Select(CreateCatalogBrand).ToList();
    }

    public static IEnumerable<CatalogItem> ParseCatalogItems(
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, int> catalogTypeIdLookup,
        IReadOnlyDictionary<string, int> catalogBrandIdLookup)
    {
        var headers = GetHeaders(lines, CatalogItemRequiredHeaders, CatalogItemOptionalHeaders);
        return lines.Skip(1)
            .Select(row => QuotedCommaSplitter.Split(row))
            .Select(columns => CreateCatalogItem(columns, headers, catalogTypeIdLookup, catalogBrandIdLookup))
            .ToList();
    }

    public static string[] GetHeaders(IReadOnlyList<string> lines, string[] requiredHeaders, string[]? optionalHeaders = null)
    {
        if (lines.Count == 0)
        {
            throw new InvalidDataException("csv file is empty");
        }

        var csvHeaders = lines[0].ToLowerInvariant().Split(',');

        if (csvHeaders.Length < requiredHeaders.Length)
        {
            throw new InvalidDataException($"requiredHeader count '{requiredHeaders.Length}' is bigger then csv header count '{csvHeaders.Length}' ");
        }

        if (optionalHeaders != null && csvHeaders.Length > requiredHeaders.Length + optionalHeaders.Length)
        {
            throw new InvalidDataException($"csv header count '{csvHeaders.Length}'  is larger then required '{requiredHeaders.Length}' and optional '{optionalHeaders.Length}' headers count");
        }

        foreach (var requiredHeader in requiredHeaders)
        {
            if (!csvHeaders.Contains(requiredHeader.ToLowerInvariant()))
            {
                throw new InvalidDataException($"does not contain required header '{requiredHeader}'");
            }
        }

        return csvHeaders;
    }

    private static CatalogType CreateCatalogType(string type)
    {
        type = type.Trim('"').Trim();
        if (string.IsNullOrEmpty(type))
        {
            throw new InvalidDataException("catalog Type Name is empty");
        }

        return new CatalogType { Type = type };
    }

    private static CatalogBrand CreateCatalogBrand(string brand)
    {
        brand = brand.Trim('"').Trim();
        if (string.IsNullOrEmpty(brand))
        {
            throw new InvalidDataException("catalog Brand Name is empty");
        }

        return new CatalogBrand { Brand = brand };
    }

    private static CatalogItem CreateCatalogItem(
        string[] column,
        string[] headers,
        IReadOnlyDictionary<string, int> catalogTypeIdLookup,
        IReadOnlyDictionary<string, int> catalogBrandIdLookup)
    {
        if (column.Length != headers.Length)
        {
            throw new InvalidDataException($"column count '{column.Length}' not the same as headers count'{headers.Length}'");
        }

        string Value(string header) => column[Array.IndexOf(headers, header)].Trim('"').Trim();

        var catalogTypeName = Value("catalogtypename");
        if (!catalogTypeIdLookup.ContainsKey(catalogTypeName))
        {
            throw new InvalidDataException($"type={catalogTypeName} does not exist in catalogTypes");
        }

        var catalogBrandName = Value("catalogbrandname");
        if (!catalogBrandIdLookup.ContainsKey(catalogBrandName))
        {
            // Message kept verbatim from legacy (it names the type, not the brand).
            throw new InvalidDataException($"type={catalogTypeName} does not exist in catalogTypes");
        }

        var priceString = Value("price");
        if (!decimal.TryParse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
        {
            throw new InvalidDataException($"price={priceString}is not a valid decimal number");
        }

        var catalogItem = new CatalogItem
        {
            CatalogTypeId = catalogTypeIdLookup[catalogTypeName],
            CatalogBrandId = catalogBrandIdLookup[catalogBrandName],
            Description = Value("description"),
            Name = Value("name"),
            Price = price,
            PictureFileName = Value("picturefilename"),
        };

        catalogItem.AvailableStock = OptionalInt(column, headers, "availablestock") ?? catalogItem.AvailableStock;
        catalogItem.RestockThreshold = OptionalInt(column, headers, "restockthreshold") ?? catalogItem.RestockThreshold;
        catalogItem.MaxStockThreshold = OptionalInt(column, headers, "maxstockthreshold") ?? catalogItem.MaxStockThreshold;

        var onReorderIndex = Array.IndexOf(headers, "onreorder");
        if (onReorderIndex != -1)
        {
            var onReorderString = column[onReorderIndex].Trim('"').Trim();
            if (!string.IsNullOrEmpty(onReorderString))
            {
                if (!bool.TryParse(onReorderString, out var onReorder))
                {
                    throw new InvalidDataException($"onReorder={onReorderString} is not a valid boolean");
                }

                catalogItem.OnReorder = onReorder;
            }
        }

        return catalogItem;
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

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"{header}={value} is not a valid integer");
        }

        return result;
    }
}
