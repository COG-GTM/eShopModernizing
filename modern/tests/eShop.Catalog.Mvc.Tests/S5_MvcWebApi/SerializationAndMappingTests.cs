using System.Text;
using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Seeding;
using eShop.Catalog.Mvc.Controllers.Api;

namespace eShop.Catalog.Mvc.Tests.S5_MvcWebApi;

public class SerializationAndMappingTests
{
    private const string LegacyBrandsJson =
        "[{\"Id\":1,\"Brand\":\"Azure\"},{\"Id\":2,\"Brand\":\".NET\"},{\"Id\":3,\"Brand\":\"Visual Studio\"}," +
        "{\"Id\":4,\"Brand\":\"SQL Server\"},{\"Id\":5,\"Brand\":\"Other\"}]";

    [Fact]
    public void Serializer_options_keep_member_names_as_declared_and_compact()
    {
        var options = LegacyWebApiJson.SerializerOptions;

        Assert.Null(options.PropertyNamingPolicy);
        Assert.Null(options.DictionaryKeyPolicy);
        Assert.False(options.WriteIndented);
    }

    [Fact]
    public void Serializer_does_not_escape_html_sensitive_characters_like_json_net()
    {
        var json = Encoding.UTF8.GetString(LegacyWebApiResult.Serialize(new BrandDto { Id = 7, Brand = "Cup<T> & 'Co' +1" }, LegacyWireFormat.Json));

        Assert.Equal("{\"Id\":7,\"Brand\":\"Cup<T> & 'Co' +1\"}", json);
    }

    [Fact]
    public void Dto_maps_id_and_brand()
    {
        var dto = BrandDto.FromModel(new CatalogBrand { Id = 42, Brand = "MECCA Max" });

        Assert.Equal(42, dto.Id);
        Assert.Equal("MECCA Max", dto.Brand);
    }

    [Fact]
    public void Dto_list_mapping_preserves_order_and_serializes_to_the_legacy_body()
    {
        var dtos = BrandDto.FromModels(PreconfiguredData.GetPreconfiguredCatalogBrands());

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, dtos.Select(d => d.Id));
        Assert.Equal(LegacyBrandsJson, Encoding.UTF8.GetString(LegacyWebApiResult.Serialize(dtos, LegacyWireFormat.Json)));
    }

    [Fact]
    public void Dto_mapping_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => BrandDto.FromModel(null!));
        Assert.Throws<ArgumentNullException>(() => BrandDto.FromModels(null!));
    }

    [Fact]
    public void Dto_xml_matches_legacy_data_contract_output()
    {
        var xml = Encoding.UTF8.GetString(LegacyWebApiResult.Serialize(new BrandDto { Id = 2, Brand = ".NET" }, LegacyWireFormat.Xml));

        Assert.Equal(
            "<CatalogBrand xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" " +
            "xmlns=\"http://schemas.datacontract.org/2004/07/eShopLegacyMVC.Models\"><Brand>.NET</Brand><Id>2</Id></CatalogBrand>",
            xml);
    }

    [Fact]
    public void Http_error_serializes_like_web_api_2()
    {
        var error = LegacyHttpError.NoController("http://localhost/api/x", "x");

        Assert.Equal(
            "{\"Message\":\"No HTTP resource was found that matches the request URI 'http://localhost/api/x'.\"," +
            "\"MessageDetail\":\"No type was found that matches the controller named 'x'.\"}",
            Encoding.UTF8.GetString(LegacyWebApiResult.Serialize(error, LegacyWireFormat.Json)));
        Assert.Equal(
            "<Error><Message>The requested resource does not support http method 'POST'.</Message></Error>",
            Encoding.UTF8.GetString(LegacyWebApiResult.Serialize(LegacyHttpError.MethodNotAllowed("POST"), LegacyWireFormat.Xml)));
    }
}
