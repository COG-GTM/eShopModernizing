using eShop.Parity;
using Xunit;

namespace eShopNet8.Mvc.IntegrationTests
{
    /// <summary>
    /// Create/Edit/Delete through the real forms: the __RequestVerificationToken hidden field and its cookie are read
    /// from the GET page and posted back on the same session (legacy MVC 5 antiforgery contract).
    /// </summary>
    public class AntiforgeryRoundTripTests
    {
        private const string App = "mvc";

        private static Dictionary<string, string> Item(string name) => new Dictionary<string, string>
        {
            ["Name"] = name, ["Description"] = "T7 round-trip", ["CatalogBrandId"] = "2", ["CatalogTypeId"] = "2",
            ["Price"] = "25.99", ["AvailableStock"] = "10", ["RestockThreshold"] = "2", ["MaxStockThreshold"] = "50",
        };

        private static async Task<(string Action, Dictionary<string, string> Data)> FormAsync(FlowRunner runner, string path)
        {
            var page = await runner.SendAsync(HttpMethod.Get, path);
            Assert.Equal(200, page.Status);
            var form = FlowRunner.CollectForm(page.Text, page.Url);
            Assert.False(string.IsNullOrEmpty(form.Data.GetValueOrDefault("__RequestVerificationToken")), $"{path} renders no __RequestVerificationToken");
            return form;
        }

        private static async Task<CapturedResponse> PostAsync(FlowRunner runner, string formPath, IDictionary<string, string> fields)
        {
            var (action, data) = await FormAsync(runner, formPath);
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

        [Fact]
        public async Task Create_with_token_round_trip_redirects_and_adds_the_item()
        {
            using var factory = new MvcAppFactory();
            using var client = factory.CreateParityClient();
            var runner = new FlowRunner(client);
            var name = "T7 Create " + Guid.NewGuid().ToString("N").Substring(0, 8);

            AssertRedirectHome(runner, await PostAsync(runner, "/Catalog/Create", Item(name)));

            var id = await CatalogBrowser.FindIdAsync(runner, App, name);
            Assert.NotNull(id);
            var details = await runner.SendAsync(HttpMethod.Get, $"/Catalog/Details/{id}");
            Assert.Equal(200, details.Status);
            Assert.Contains(name, details.Text);
        }

        [Fact]
        public async Task Create_without_token_is_rejected_with_500_like_legacy()
        {
            using var factory = new MvcAppFactory();
            using var client = factory.CreateParityClient();
            var runner = new FlowRunner(client);
            var name = "T7 NoToken " + Guid.NewGuid().ToString("N").Substring(0, 8);
            var (action, data) = await FormAsync(runner, "/Catalog/Create");
            data.Remove("__RequestVerificationToken");
            foreach (var kv in Item(name))
            {
                data[kv.Key] = kv.Value;
            }

            var response = await runner.SendAsync(HttpMethod.Post, action, data);

            Assert.Equal(500, response.Status);
            Assert.Null(await CatalogBrowser.FindIdAsync(runner, App, name));
        }

        [Fact]
        public async Task Token_from_another_session_is_rejected()
        {
            using var factory = new MvcAppFactory();
            using var issuer = factory.CreateParityClient();
            using var attacker = factory.CreateParityClient();
            var name = "T7 Forged " + Guid.NewGuid().ToString("N").Substring(0, 8);
            var (action, data) = await FormAsync(new FlowRunner(issuer), "/Catalog/Create");
            foreach (var kv in Item(name))
            {
                data[kv.Key] = kv.Value;
            }
            var runner = new FlowRunner(attacker);

            var response = await runner.SendAsync(HttpMethod.Post, action, data);

            Assert.True(response.Status >= 400, $"cross-session token accepted with {response.Status}");
            Assert.Null(await CatalogBrowser.FindIdAsync(runner, App, name));
        }

        [Fact]
        public async Task Edit_with_token_round_trip_updates_the_item()
        {
            using var factory = new MvcAppFactory();
            using var client = factory.CreateParityClient();
            var runner = new FlowRunner(client);
            var name = "T7 Edit " + Guid.NewGuid().ToString("N").Substring(0, 8);
            AssertRedirectHome(runner, await PostAsync(runner, "/Catalog/Create", Item(name)));
            var id = await CatalogBrowser.FindIdAsync(runner, App, name);

            var renamed = name + " v2";
            AssertRedirectHome(runner, await PostAsync(runner, $"/Catalog/Edit/{id}",
                new Dictionary<string, string> { ["Name"] = renamed, ["Price"] = "21.75", ["AvailableStock"] = "75" }));

            var details = await runner.SendAsync(HttpMethod.Get, $"/Catalog/Details/{id}");
            Assert.Contains(renamed, details.Text);
            Assert.Contains("21.75", details.Text);
        }

        [Fact]
        public async Task Delete_with_token_round_trip_removes_the_item()
        {
            using var factory = new MvcAppFactory();
            using var client = factory.CreateParityClient();
            var runner = new FlowRunner(client);
            var name = "T7 Delete " + Guid.NewGuid().ToString("N").Substring(0, 8);
            AssertRedirectHome(runner, await PostAsync(runner, "/Catalog/Create", Item(name)));
            var id = await CatalogBrowser.FindIdAsync(runner, App, name);

            AssertRedirectHome(runner, await PostAsync(runner, $"/Catalog/Delete/{id}", new Dictionary<string, string>()));

            Assert.Null(await CatalogBrowser.FindIdAsync(runner, App, name));
        }
    }
}
