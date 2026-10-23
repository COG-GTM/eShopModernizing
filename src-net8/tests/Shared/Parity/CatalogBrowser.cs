using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace eShop.Parity
{
    /// <summary>Reads the catalog through the app's own list page (works in-process and against a live server).</summary>
    public static class CatalogBrowser
    {
        private static readonly Regex DetailsId = new Regex(@"/Catalog/Details/(\d+)", RegexOptions.IgnoreCase);

        public static async Task<List<JsonObject>> RowsAsync(FlowRunner runner, string app)
        {
            var state = await runner.DataStateAsync(app);
            return state["rows"].AsArray().Select(r => r.AsObject()).ToList();
        }

        /// <summary>Id of the first row whose cells contain <paramref name="text"/>, or null.</summary>
        public static async Task<int?> FindIdAsync(FlowRunner runner, string app, string text)
        {
            foreach (var row in await RowsAsync(runner, app))
            {
                if (row["cells"].AsArray().Any(c => ((string)c)?.Contains(text) == true))
                {
                    var link = row["links"].AsArray().Select(l => (string)l).Select(l => DetailsId.Match(l ?? string.Empty)).First(m => m.Success);
                    return int.Parse(link.Groups[1].Value);
                }
            }
            return null;
        }
    }
}
