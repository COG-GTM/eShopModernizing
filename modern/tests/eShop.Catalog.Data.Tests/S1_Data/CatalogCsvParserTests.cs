using eShop.Catalog.Data.Seeding;

namespace eShop.Catalog.Data.Tests.S1_Data;

public class CatalogCsvParserTests
{
    private static readonly Dictionary<string, int> Types = new() { ["Mug"] = 1, ["T-Shirt"] = 2, ["Sheet"] = 3 };
    private static readonly Dictionary<string, int> Brands = new() { [".NET"] = 2, ["Other"] = 5 };

    [Fact]
    public void Parses_types_and_brands_trimming_quotes_and_spaces()
    {
        var types = CatalogCsvParser.ParseCatalogTypes(new[] { "CatalogType", "Mug", "\"T-Shirt\"", "Sheet " }).ToList();
        var brands = CatalogCsvParser.ParseCatalogBrands(new[] { "CatalogBrand", "Azure", ".NET ", "\" Visual Studio \"" }).ToList();

        Assert.Equal(new[] { "Mug", "T-Shirt", "Sheet" }, types.Select(t => t.Type));
        Assert.Equal(new[] { "Azure", ".NET", "Visual Studio" }, brands.Select(b => b.Brand));
        Assert.All(types, t => Assert.Equal(0, t.Id));
    }

    [Fact]
    public void Empty_type_or_brand_row_throws_legacy_message()
    {
        Assert.Equal("catalog Type Name is empty",
            Assert.Throws<InvalidDataException>(() => CatalogCsvParser.ParseCatalogTypes(new[] { "CatalogType", "Mug", "" })).Message);
        Assert.Equal("catalog Brand Name is empty",
            Assert.Throws<InvalidDataException>(() => CatalogCsvParser.ParseCatalogBrands(new[] { "CatalogBrand", "  " })).Message);
    }

    [Fact]
    public void Parses_items_like_legacy_setup_file_including_quoted_commas_and_optional_columns()
    {
        var lines = new[]
        {
            "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName,availablestock,onreorder",
            "T-Shirt,.NET,\".NET Bot Black Hoodie, and more\",.NET Bot Black Hoodie,19.5,1.png,100,false",
            "Mug,.NET,.NET Black & White Mug,.NET Black & White Mug,8.50,2.png,89,true",
            "T-Shirt,Other,Prism White T-Shirt,Prism White T-Shirt,12,3.png,,",
        };

        var items = CatalogCsvParser.ParseCatalogItems(lines, Types, Brands).ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal(".NET Bot Black Hoodie, and more", items[0].Description);
        Assert.Equal((2, 2, 19.5M, "1.png", 100, false), (items[0].CatalogTypeId, items[0].CatalogBrandId, items[0].Price, items[0].PictureFileName, items[0].AvailableStock, items[0].OnReorder));
        Assert.Equal((1, 8.50M, 89, true), (items[1].CatalogTypeId, items[1].Price, items[1].AvailableStock, items[1].OnReorder));
        Assert.Equal((5, 0, false), (items[2].CatalogBrandId, items[2].AvailableStock, items[2].OnReorder));
        Assert.All(items, i => Assert.Equal(0, i.Id));
    }

    [Fact]
    public void Header_names_are_case_insensitive_and_column_order_is_free()
    {
        var lines = new[]
        {
            "price,NAME,description,picturefilename,catalogbrandname,catalogtypename,RestockThreshold,MaxStockThreshold",
            "7.25,Lip Oil,Gloss,lip.png,Other,Sheet,5,50",
        };

        var item = Assert.Single(CatalogCsvParser.ParseCatalogItems(lines, Types, Brands));

        Assert.Equal(("Lip Oil", 7.25M, 3, 5, 5, 50), (item.Name, item.Price, item.CatalogTypeId, item.CatalogBrandId, item.RestockThreshold, item.MaxStockThreshold));
    }

    [Theory]
    [InlineData("CatalogTypeName,CatalogBrandName", "requiredHeader count '6' is bigger then csv header count '2' ")]
    [InlineData("CatalogTypeName,CatalogBrandName,Description,Name,Price,Picture", "does not contain required header 'pictureFileName'")]
    [InlineData("CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName,a,b,c,d,e", "csv header count '11'  is larger then required '6' and optional '4' headers count")]
    public void Invalid_item_headers_throw_legacy_messages(string header, string expected)
    {
        var ex = Assert.Throws<InvalidDataException>(() => CatalogCsvParser.ParseCatalogItems(new[] { header }, Types, Brands));

        Assert.Equal(expected, ex.Message);
    }

    [Theory]
    [InlineData("Hat,.NET,d,n,1,p.png", "type=Hat does not exist in catalogTypes")]
    [InlineData("Mug,Acme,d,n,1,p.png", "type=Mug does not exist in catalogTypes")]
    [InlineData("Mug,.NET,d,n,abc,p.png", "price=abcis not a valid decimal number")]
    [InlineData("Mug,.NET,d,n,-1,p.png", "price=-1is not a valid decimal number")]
    [InlineData("Mug,.NET,d,n,1", "column count '5' not the same as headers count'6'")]
    public void Invalid_item_rows_throw_legacy_messages(string row, string expected)
    {
        var lines = new[] { "CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName", row };

        var ex = Assert.Throws<InvalidDataException>(() => CatalogCsvParser.ParseCatalogItems(lines, Types, Brands));

        Assert.Equal(expected, ex.Message);
    }

    [Theory]
    [InlineData("availablestock", "x", "availablestock=x is not a valid integer")]
    [InlineData("onreorder", "maybe", "onReorder=maybe is not a valid boolean")]
    public void Invalid_optional_values_throw(string header, string value, string expected)
    {
        var lines = new[] { $"CatalogTypeName,CatalogBrandName,Description,Name,Price,PictureFileName,{header}", $"Mug,.NET,d,n,1,p.png,{value}" };

        Assert.Equal(expected, Assert.Throws<InvalidDataException>(() => CatalogCsvParser.ParseCatalogItems(lines, Types, Brands)).Message);
    }

    [Fact]
    public void Legacy_setup_csv_files_parse()
    {
        var setup = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../eShopLegacyWebFormsSolution/src/eShopLegacyWebForms/Setup"));
        Assert.True(Directory.Exists(setup), setup);

        var types = CatalogCsvParser.ParseCatalogTypes(File.ReadAllLines(Path.Combine(setup, "CatalogTypes.csv"))).ToList();
        var brands = CatalogCsvParser.ParseCatalogBrands(File.ReadAllLines(Path.Combine(setup, "CatalogBrands.csv"))).ToList();
        var typeIds = types.Select((t, i) => (t.Type, Id: i + 1)).ToDictionary(x => x.Type, x => x.Id);
        var brandIds = brands.Select((b, i) => (b.Brand, Id: i + 1)).ToDictionary(x => x.Brand, x => x.Id);
        var items = CatalogCsvParser.ParseCatalogItems(File.ReadAllLines(Path.Combine(setup, "CatalogItems.csv")), typeIds, brandIds).ToList();

        Assert.Equal(new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" }, types.Take(4).Select(t => t.Type));
        Assert.Equal(new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" }, brands.Take(5).Select(b => b.Brand));
        Assert.NotEmpty(items);
        Assert.Equal(".NET Bot Black Hoodie, and more", items[0].Description);
    }
}
