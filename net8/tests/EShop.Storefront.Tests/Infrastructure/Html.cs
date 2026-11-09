using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace EShop.Storefront.Tests.Infrastructure;

/// <summary>C# port of modernization/tools/tests/helpers.py.</summary>
public static partial class Html
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static string Collapse(string? text) => Whitespace().Replace(text ?? string.Empty, " ").Trim();

    /// <summary>Catalog table rows: every &lt;tr&gt; with at least 10 &lt;td&gt; cells.</summary>
    public static List<List<string>> Rows(Page page) =>
        page.Html.QuerySelectorAll("tr")
            .Select(tr => tr.QuerySelectorAll("td").Select(td => Collapse(td.TextContent)).ToList())
            .Where(cells => cells.Count >= 10)
            .ToList();

    /// <summary>Visible validation messages.</summary>
    public static List<string> Errors(Page page) =>
        page.Html.QuerySelectorAll(".field-validation-error, .text-danger")
            .Where(el =>
            {
                var style = (el.GetAttribute("style") ?? string.Empty).Replace(" ", string.Empty);
                return !style.Contains("visibility:hidden") && !style.Contains("display:none");
            })
            .Select(el => Collapse(el.TextContent))
            .Where(t => t.Length > 0)
            .ToList();

    public static IEnumerable<string> Names(Page page, string selector = "input, select") =>
        page.Html.QuerySelectorAll(selector).Select(e => e.GetAttribute("name")).OfType<string>();

    public static IEnumerable<int> DetailIds(Page page) =>
        page.Html.QuerySelectorAll("a")
            .Select(a => a.GetAttribute("href") ?? string.Empty)
            .Where(h => h.Contains("/Catalog/Details/"))
            .Select(h => int.Parse(h[(h.LastIndexOf('/') + 1)..]));

    /// <summary>GET the page, fill its first POST form like a browser, override/drop fields, press a submit button, POST.</summary>
    public static async Task<Page> FormPostAsync(StorefrontClient client, string pagePath,
        IReadOnlyDictionary<string, string>? fields = null, IEnumerable<string>? drop = null, string? submit = null)
    {
        var page = await client.GetAsync(pagePath);
        if (page.Status != 200)
        {
            throw new InvalidOperationException($"form page {pagePath} returned {page.Status}");
        }

        var form = page.Html.QuerySelectorAll("form")
            .FirstOrDefault(f => string.Equals(f.GetAttribute("method"), "post", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"no POST form on {pagePath}");

        var data = new Dictionary<string, string?>();
        foreach (var el in form.QuerySelectorAll("input, select, textarea"))
        {
            var name = el.GetAttribute("name");
            var type = (el.GetAttribute("type") ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrEmpty(name) || (el.LocalName == "input" && type is "submit" or "button"))
            {
                continue;
            }

            if (el.LocalName == "select")
            {
                var option = el.QuerySelectorAll("option").FirstOrDefault(o => o.HasAttribute("selected"))
                             ?? el.QuerySelector("option");
                data[name] = option?.GetAttribute("value");
            }
            else
            {
                data[name] = el.GetAttribute("value") ?? string.Empty;
            }
        }

        string Key(string k) => data.ContainsKey(k) ? k : data.Keys.FirstOrDefault(n => n.EndsWith("$" + k)) ?? k;

        foreach (var (k, v) in fields ?? new Dictionary<string, string>())
        {
            data[Key(k)] = v;
        }

        foreach (var k in drop ?? [])
        {
            data.Remove(Key(k));
        }

        if (submit is not null)
        {
            var button = form.QuerySelectorAll("input")
                .First(i => i.GetAttribute("type") == "submit" && i.GetAttribute("value") == submit);
            data[button.GetAttribute("name")!] = submit;
        }

        var action = form.GetAttribute("action");
        var target = string.IsNullOrEmpty(action) ? page.Url : new Uri(page.Url, action);
        return await client.PostFormAsync(target,
            data.Where(kv => kv.Value is not null).Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!)));
    }
}
