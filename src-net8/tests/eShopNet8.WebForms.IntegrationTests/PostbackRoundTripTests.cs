using eShop.Parity;
using Xunit;

namespace eShopNet8.WebForms.IntegrationTests
{
    /// <summary>
    /// Create/Edit/Delete through the real pages with the legacy WebForms field names (ctl00$MainContent$...): every
    /// hidden field the GET page renders (antiforgery / __VIEWSTATE equivalents) is posted back on the same session.
    /// </summary>
    public class PostbackRoundTripTests
    {
        private const string App = "webforms";
        private const string Prefix = "ctl00$MainContent$";

        private static async Task<CapturedResponse> PostAsync(FlowRunner runner, string formPath, IDictionary<string, string> fields)
        {
            var page = await runner.SendAsync(HttpMethod.Get, formPath);
            Assert.Equal(200, page.Status);
            var (action, data) = FlowRunner.CollectForm(page.Text, page.Url);
            foreach (var kv in fields)
            {
                data[kv.Key] = kv.Value;
            }
            return await runner.SendAsync(HttpMethod.Post, action, data);
        }

        private static void AssertRedirectHome(FlowRunner runner, CapturedResponse response)
        {
            Assert.Equal(302, response.Status);
            Assert.Equal("{base}/", runner.Normaliser.Url(response.Location, response.Url));
        }

        private static Dictionary<string, string> Item(string name) => new Dictionary<string, string>
        {
            [Prefix + "Name"] = name, [Prefix + "Description"] = "T7 round-trip", [Prefix + "Brand"] = "2", [Prefix + "Type"] = "1",
            [Prefix + "Price"] = "9.99", [Prefix + "Stock"] = "20", [Prefix + "Restock"] = "5", [Prefix + "Maxstock"] = "40",
            [Prefix + "ctl06"] = "[ Create ]",
        };

        private static async Task<int> CreateAsync(FlowRunner runner, string name)
        {
            AssertRedirectHome(runner, await PostAsync(runner, "/Catalog/Create", Item(name)));
            var id = await CatalogBrowser.FindIdAsync(runner, App, name);
            Assert.True(id.HasValue, $"{name} not listed after create");
            return id.Value;
        }

        [Fact]
        public async Task Create_postback_redirects_and_adds_the_item()
        {
            using var factory = new WebFormsAppFactory();
            using var client = factory.CreateParityClient();
            var runner = new FlowRunner(client);
            var name = "T7 Create " + Guid.NewGuid().ToString("N").Substring(0, 8);

            var id = await CreateAsync(runner, name);

            var details = await runner.SendAsync(HttpMethod.Get, $"/Catalog/Details/{id}");
            Assert.Equal(200, details.Status);
            Assert.Contains(name, details.Text);
        }

        [Fact]
        public async Task Edit_postback_updates_a_seeded_item()
        {
            // Item 3 like wf-edit-post-valid: Mono's legacy Edit.aspx only round-trips items whose navigations are composed.
            using var factory = new WebFormsAppFactory();
            using var client = factory.CreateParityClient();
            var runner = new FlowRunner(client);
            var name = "T7 Edit " + Guid.NewGuid().ToString("N").Substring(0, 8);

            AssertRedirectHome(runner, await PostAsync(runner, "/Catalog/Edit/3", new Dictionary<string, string>
            {
                [Prefix + "Name"] = name, [Prefix + "Price"] = "14.25", [Prefix + "TypeDropDownList"] = "3", [Prefix + "ctl07"] = "[ Save ]",
            }));

            // Asserted on the list: legacy Details/3 can serve a stale render on Mono after earlier GETs of the same page.
            Assert.Equal(3, await CatalogBrowser.FindIdAsync(runner, App, name));
            var row = (await CatalogBrowser.RowsAsync(runner, App)).Single(r => r["cells"].AsArray().Any(c => (string)c == name));
            Assert.Contains("14.25", row["cells"].AsArray().Select(c => (string)c));
        }

        [Fact]
        public async Task Delete_postback_removes_the_item()
        {
            using var factory = new WebFormsAppFactory();
            using var client = factory.CreateParityClient();
            var runner = new FlowRunner(client);
            var name = "T7 Delete " + Guid.NewGuid().ToString("N").Substring(0, 8);
            var id = await CreateAsync(runner, name);

            AssertRedirectHome(runner, await PostAsync(runner, $"/Catalog/Delete/{id}",
                new Dictionary<string, string> { [Prefix + "ctl11"] = "[ Delete ]" }));

            Assert.Null(await CatalogBrowser.FindIdAsync(runner, App, name));
        }
    }
}
