using EShop.Storefront.Tests.Infrastructure;
using Xunit;
using static EShop.Storefront.Tests.Infrastructure.Html;

namespace EShop.Storefront.Tests.Mvc;

/// <summary>Mirrors test_mvc.py::test_zz_create_edit_delete_roundtrip on its own freshly seeded app.</summary>
public class MvcRoundtripTests(MvcAppFixture app) : IClassFixture<MvcAppFixture>
{
    private const string AllItems = "/Catalog/Index?pageSize=100&pageIndex=0";

    [SkippableFact]
    public async Task Create_edit_delete_roundtrip()
    {
        app.Require(Slice.CatalogData, Slice.MvcCatalog);
        var mvc = app.Client;

        var r = await FormPostAsync(mvc, "/Catalog/Create", MvcCatalogTests.Valid);
        Assert.Equal(302, r.Status);
        Assert.EndsWith("/", r.Location);

        var all = Rows(await mvc.GetAsync(AllItems));
        Assert.Equal(13, all.Count);
        Assert.Equal(["Characterization Item", "char", ".NET", "Mug", "$12.34"], all[^1].Skip(1).Take(5));
        var newId = DetailIds(await mvc.GetAsync("/Catalog/Index?pageSize=100")).Max();
        Assert.Equal(13, newId);

        r = await FormPostAsync(mvc, $"/Catalog/Edit/{newId}", new Dictionary<string, string> { ["Name"] = "" });
        Assert.Equal(200, r.Status);
        Assert.Contains("The Name field is required.", Errors(r));

        r = await FormPostAsync(mvc, $"/Catalog/Edit/{newId}",
            new Dictionary<string, string> { ["Name"] = "Characterization Edited", ["Price"] = "5.00" });
        Assert.Equal(302, r.Status);
        Assert.Contains("Characterization Edited", (await mvc.GetAsync($"/Catalog/Details/{newId}")).Text);

        r = await FormPostAsync(mvc, $"/Catalog/Delete/{newId}");
        Assert.Equal(302, r.Status);
        Assert.Equal(404, (await mvc.GetAsync($"/Catalog/Details/{newId}")).Status);
        Assert.Equal(12, Rows(await mvc.GetAsync(AllItems)).Count);
    }
}
