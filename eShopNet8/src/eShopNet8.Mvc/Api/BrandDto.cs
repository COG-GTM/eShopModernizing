using System.Runtime.Serialization;
using eShop.Catalog.Core.Models;

namespace eShopNet8.Mvc.Api;

// Wire shape of a brand on /api/brands and /api/files: {"Id":1,"Brand":"Azure"} (PascalCase, like Web API 2 / Json.NET).
// The data contract name/namespace reproduce the legacy XML representation (ArrayOfCatalogBrand in the
// eShopLegacyMVC.Models data contract namespace) for clients that negotiate application/xml (INVENTORY Q16).
[DataContract(Name = "CatalogBrand", Namespace = "http://schemas.datacontract.org/2004/07/eShopLegacyMVC.Models")]
public sealed class BrandDto
{
    [DataMember]
    public int Id { get; init; }

    [DataMember]
    public string Brand { get; init; } = null!;

    public static BrandDto From(CatalogBrand brand) => new() { Id = brand.Id, Brand = brand.Brand };
}
