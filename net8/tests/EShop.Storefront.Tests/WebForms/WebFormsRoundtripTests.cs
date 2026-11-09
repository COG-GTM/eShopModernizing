using EShop.Storefront.Tests.Infrastructure;
using Xunit;
using static EShop.Storefront.Tests.Infrastructure.Html;

namespace EShop.Storefront.Tests.WebForms;

/// <summary>Mirrors test_webforms.py::test_zz_create_edit_delete_roundtrip on its own freshly seeded app.</summary>
public class WebFormsRoundtripTests(WebFormsAppFixture app) : IClassFixture<WebFormsAppFixture>
{
    private const string AllItems = "/Default/index/0/size/100";

    [SkippableFact]
    public async Task Create_edit_delete_roundtrip()
    {
        app.Require(Slice.CatalogData, Slice.RazorCatalog);
        var wf = app.Client;

        var r = await FormPostAsync(wf, "/Catalog/Create", WebFormsCatalogTests.Valid, submit: "[ Create ]");
        Assert.Equal(302, r.Status);
        Assert.EndsWith("/", r.Location);

        var all = Rows(await wf.GetAsync(AllItems));
        Assert.Equal(13, all.Count);
        Assert.Equal(["Characterization Item", "char", ".NET"], all[^1].Skip(1).Take(3));
        var newId = DetailIds(await wf.GetAsync(AllItems)).Max();

        r = await FormPostAsync(wf, $"/Catalog/Edit/{newId}", new Dictionary<string, string> { ["Name"] = "" }, submit: "[ Save ]");
        Assert.Equal(200, r.Status);
        Assert.Contains("The Name field is required.", Errors(r));

        r = await FormPostAsync(wf, $"/Catalog/Edit/{newId}", new Dictionary<string, string> { ["Name"] = "Characterization Edited" },
            submit: "[ Save ]");
        Assert.Equal(302, r.Status);
        // Legacy mock quirk: Details of a created-then-edited item 500s on Mono (CONTRACT.md section 5); assert via the list.
        Assert.Contains(Rows(await wf.GetAsync(AllItems)), row => row[1] == "Characterization Edited");

        r = await FormPostAsync(wf, $"/Catalog/Delete/{newId}", submit: "[ Delete ]");
        Assert.Equal(302, r.Status);
        Assert.Equal(12, Rows(await wf.GetAsync(AllItems)).Count);
    }
}
