using System.Runtime.Serialization;
using eShop.Catalog.Data.Models;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>
/// Wire shape of a brand on /api/brands and /api/files: JSON <c>{"Id":1,"Brand":"Azure"}</c>, XML element
/// <c>CatalogBrand</c> in the legacy data-contract namespace (members alphabetical, as DataContractSerializer emitted).
/// </summary>
[DataContract(Name = "CatalogBrand", Namespace = LegacyDataContractNamespace)]
public sealed class BrandDto
{
    public const string LegacyDataContractNamespace = "http://schemas.datacontract.org/2004/07/eShopLegacyMVC.Models";

    [DataMember]
    public int Id { get; set; }

    [DataMember]
    public string? Brand { get; set; }

    public static BrandDto FromModel(CatalogBrand brand)
    {
        ArgumentNullException.ThrowIfNull(brand);
        return new BrandDto { Id = brand.Id, Brand = brand.Brand };
    }

    public static List<BrandDto> FromModels(IEnumerable<CatalogBrand> brands)
    {
        ArgumentNullException.ThrowIfNull(brands);
        return brands.Select(FromModel).ToList();
    }
}
