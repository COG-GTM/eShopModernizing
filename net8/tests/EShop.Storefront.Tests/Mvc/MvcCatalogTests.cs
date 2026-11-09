using EShop.Storefront.Tests.Infrastructure;
using Xunit;
using static EShop.Storefront.Tests.Infrastructure.Html;

namespace EShop.Storefront.Tests.Mvc;

/// <summary>Mirrors modernization/tools/tests/test_mvc.py: list, paging, details, errors, create form + validation, antiforgery.</summary>
public class MvcCatalogTests(MvcAppFixture app) : IClassFixture<MvcAppFixture>
{
    internal static readonly IReadOnlyDictionary<string, string> Valid = new Dictionary<string, string>
    {
        ["Name"] = "Characterization Item", ["Description"] = "char", ["Price"] = "12.34", ["CatalogBrandId"] = "2",
        ["CatalogTypeId"] = "1", ["AvailableStock"] = "10", ["RestockThreshold"] = "2", ["MaxStockThreshold"] = "50",
    };

    private StorefrontClient Mvc => app.Client;

    [SkippableFact]
    public async Task List_default_shows_first_10_ordered_by_id()
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var r = await Mvc.GetAsync("/");
        Assert.Equal(200, r.Status);
        Assert.Contains("text/html", r.ContentType);
        var rows = Rows(r);
        Assert.Equal(10, rows.Count);
        Assert.Equal(".NET Bot Black Hoodie", rows[0][1]);
        Assert.Equal("$19.50", rows[0][5]);
    }

    [SkippableTheory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 1, 2)]
    [InlineData(10, 2, 0)]
    [InlineData(5, 0, 5)]
    [InlineData(5, 2, 2)]
    [InlineData(12, 0, 12)]
    [InlineData(20, 0, 12)]
    [InlineData(1, 11, 1)]
    [InlineData(3, 3, 3)]
    public async Task List_paging(int size, int index, int count)
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var r = await Mvc.GetAsync($"/Catalog/Index?pageSize={size}&pageIndex={index}");
        Assert.Equal(200, r.Status);
        Assert.Equal(count, Rows(r).Count);
    }

    [SkippableFact]
    public async Task List_page_size_zero_is_server_error()
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        Assert.Equal(500, (await Mvc.GetAsync("/Catalog/Index?pageSize=0&pageIndex=0")).Status);
    }

    [SkippableFact]
    public async Task List_nonnumeric_params_fall_back_to_defaults()
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        Assert.Equal(10, Rows(await Mvc.GetAsync("/Catalog/Index?pageSize=abc&pageIndex=xyz")).Count);
    }

    [SkippableTheory]
    [InlineData(1, ".NET Bot Black Hoodie")]
    [InlineData(5, "Roslyn Red Sheet")]
    public async Task Details(int item, string name)
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var r = await Mvc.GetAsync($"/Catalog/Details/{item}");
        Assert.Equal(200, r.Status);
        Assert.Contains(name, r.Text);
    }

    [SkippableTheory]
    [InlineData("/Catalog/Details/999", 404)]
    [InlineData("/Catalog/Details", 400)]
    [InlineData("/Catalog/Edit/999", 404)]
    [InlineData("/Catalog/Edit", 400)]
    [InlineData("/Catalog/Delete/999", 404)]
    [InlineData("/Catalog/Delete", 400)]
    [InlineData("/does-not-exist", 404)]
    [InlineData("/Catalog/NoSuchAction", 404)]
    public async Task Missing_and_bad_requests(string path, int status)
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        Assert.Equal(status, (await Mvc.GetAsync(path)).Status);
    }

    [SkippableFact]
    public async Task Create_get_form()
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var r = await Mvc.GetAsync("/Catalog/Create");
        Assert.Equal(200, r.Status);
        var names = Names(r).ToHashSet();
        Assert.Subset(names, new HashSet<string>
        {
            "Name", "Description", "CatalogBrandId", "CatalogTypeId", "Price", "AvailableStock", "RestockThreshold",
            "MaxStockThreshold", "__RequestVerificationToken",
        });
        var brands = r.Html.QuerySelectorAll("select[name=CatalogBrandId] option").Select(o => o.TextContent.Trim());
        Assert.Equal(["Azure", ".NET", "Visual Studio", "SQL Server", "Other"], brands);
    }

    [SkippableTheory]
    [InlineData("Name", "", "The Name field is required.")]
    [InlineData("Price", "1.234", "The field Price must be a positive number with maximum two decimals.")]
    [InlineData("Price", "2000000", "The field Price must be between 0 and 1000000.")]
    [InlineData("Price", "abc", "The value 'abc' is not valid for Price.")]
    [InlineData("Price", "", "The Price field is required.")]
    [InlineData("AvailableStock", "-1", "The field Stock must be between 0 and 10 million.")]
    [InlineData("RestockThreshold", "20000000", "The field Restock must be between 0 and 10 million.")]
    [InlineData("MaxStockThreshold", "abc", "The value 'abc' is not valid for Max stock.")]
    public async Task Create_validation(string field, string value, string message)
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var r = await FormPostAsync(Mvc, "/Catalog/Create", new Dictionary<string, string>(Valid) { [field] = value });
        Assert.Equal(200, r.Status);
        Assert.Contains(message, Errors(r));
    }

    [SkippableFact]
    public async Task Create_without_antiforgery_token_is_rejected_with_500()
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var r = await FormPostAsync(Mvc, "/Catalog/Create", Valid, drop: ["__RequestVerificationToken"]);
        Assert.Equal(500, r.Status); // legacy MVC 5 HttpAntiForgeryException; CONTRACT.md section 5: reproduce 500
    }

    // MVC 5 emitted data-val-number for int properties too; Core only does it for floating-point types.
    [SkippableTheory]
    [InlineData("/Catalog/Create", "AvailableStock", "The field Stock must be a number.")]
    [InlineData("/Catalog/Create", "RestockThreshold", "The field Restock must be a number.")]
    [InlineData("/Catalog/Create", "MaxStockThreshold", "The field Max stock must be a number.")]
    [InlineData("/Catalog/Edit/1", "Id", "The field Id must be a number.")]
    public async Task Integer_inputs_carry_client_number_validation(string path, string field, string message)
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var r = await Mvc.GetAsync(path);
        Assert.Equal(200, r.Status);
        Assert.Equal(message, r.Html.QuerySelector($"input[name={field}]")!.GetAttribute("data-val-number"));
    }
}
