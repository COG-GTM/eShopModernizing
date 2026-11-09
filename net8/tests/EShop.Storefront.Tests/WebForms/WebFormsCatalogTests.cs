using EShop.Storefront.Tests.Infrastructure;
using Xunit;
using static EShop.Storefront.Tests.Infrastructure.Html;

namespace EShop.Storefront.Tests.WebForms;

/// <summary>Mirrors modernization/tools/tests/test_webforms.py: Default list/paging routes, details, missing ids, create form + validation.</summary>
public class WebFormsCatalogTests(WebFormsAppFixture app) : IClassFixture<WebFormsAppFixture>
{
    internal static readonly IReadOnlyDictionary<string, string> Valid = new Dictionary<string, string>
    {
        ["Name"] = "Characterization Item", ["Description"] = "char", ["Brand"] = "2", ["Type"] = "1", ["Price"] = "12.34",
        ["Stock"] = "10", ["Restock"] = "2", ["Maxstock"] = "50",
    };

    private StorefrontClient Wf => app.Client;

    [SkippableFact]
    public async Task List_default()
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        var r = await Wf.GetAsync("/");
        Assert.Equal(200, r.Status);
        var rows = Rows(r);
        Assert.Equal(10, rows.Count);
        Assert.Equal(".NET Bot Black Hoodie", rows[0][1]);
    }

    [SkippableTheory]
    [InlineData("/Default")]
    [InlineData("/Default.aspx")]
    public async Task List_aliases(string path)
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        Assert.Equal(10, Rows(await Wf.GetAsync(path)).Count);
    }

    [SkippableTheory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 1, 2)]
    [InlineData(5, 0, 5)]
    [InlineData(5, 2, 2)]
    [InlineData(12, 0, 12)]
    [InlineData(20, 0, 12)]
    [InlineData(1, 11, 1)]
    [InlineData(3, 3, 3)]
    public async Task List_paging_route(int size, int index, int count)
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        var r = await Wf.GetAsync($"/Default/index/{index}/size/{size}");
        Assert.Equal(200, r.Status);
        Assert.Equal(count, Rows(r).Count);
    }

    [SkippableTheory]
    [InlineData(1, ".NET Bot Black Hoodie")]
    [InlineData(12, "Prism White TShirt")]
    public async Task Details(int item, string name)
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        var r = await Wf.GetAsync($"/Catalog/Details/{item}");
        Assert.Equal(200, r.Status);
        Assert.Contains(name, r.Text);
    }

    [SkippableTheory]
    [InlineData("/Catalog/Details/999", 500)]
    [InlineData("/Catalog/Edit/999", 500)]
    [InlineData("/Catalog/Delete/999", 500)]
    [InlineData("/does-not-exist", 404)]
    public async Task Missing(string path, int status)
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        Assert.Equal(status, (await Wf.GetAsync(path)).Status); // CONTRACT.md section 5: WebForms missing id -> 500
    }

    [SkippableFact]
    public async Task Create_get_form()
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        var r = await Wf.GetAsync("/Catalog/Create");
        Assert.Equal(200, r.Status);
        var names = Names(r).Select(n => n[(n.LastIndexOf('$') + 1)..]).ToHashSet();
        Assert.Subset(names, new HashSet<string> { "Name", "Description", "Brand", "Type", "Price", "Stock", "Restock", "Maxstock" });
    }

    [SkippableTheory]
    [InlineData("Name", "", "The Name field is required.")]
    [InlineData("Price", "-5", "The Price must be a positive number with maximum two decimals between 0 and 1 million.")]
    [InlineData("Price", "abc", "The Price must be a positive number with maximum two decimals between 0 and 1 million.")]
    [InlineData("Stock", "-1", "The field Stock must be between 0 and 10 million.")]
    [InlineData("Restock", "abc", "The field Restock must be between 0 and 10 million.")]
    [InlineData("Maxstock", "20000000", "The field Max stock must be between 0 and 10 million.")]
    public async Task Create_validation(string field, string value, string message)
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        var r = await FormPostAsync(Wf, "/Catalog/Create", new Dictionary<string, string>(Valid) { [field] = value }, submit: "[ Create ]");
        Assert.Equal(200, r.Status);
        Assert.Contains(message, Errors(r));
    }
}
