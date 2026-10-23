using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace eShopNet8.IntegrationTests.Parity;

public sealed record FlowSpec(JsonObject Raw, string SequenceId, string App)
{
    public string Id => (string)Raw["id"]!;
    public string Method => ((string?)Raw["method"] ?? "GET").ToUpperInvariant();
    public string? Path => (string?)Raw["path"];
    public string? Quirk => (string?)Raw["quirk"];
    public string Compare => (string?)Raw["compare"] ?? "full";
    public bool Snapshot => Raw["snapshot"] is JsonValue v && v.GetValue<bool>();
    public JsonObject? Form => Raw["form"] as JsonObject;
    public bool IsApi => Path is not null && (Path == "/api" || Path.StartsWith("/api/", StringComparison.Ordinal));
}

// C# port of modernization/parity/runner.py: executes one flow on a cookie session and normalises the response.
public static partial class FlowRunner
{
    public const string UserAgent = "eshop-parity-harness/1";

    [GeneratedRegex(@"/Catalog/(?:Edit|Details|Delete)/(\d+)")]
    private static partial Regex CatalogIdRe();

    [GeneratedRegex(@"[?&]id=(\d+)")]
    private static partial Regex QueryIdRe();

    private static HttpRequestMessage NewRequest(HttpMethod method, string url, IReadOnlyDictionary<string, string>? headers = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        var h = headers ?? new Dictionary<string, string>();
        if (!h.Keys.Any(k => k.Equals("Accept", StringComparison.OrdinalIgnoreCase)))
            req.Headers.TryAddWithoutValidation("Accept", "*/*");
        foreach (var (k, v) in h)
            req.Headers.TryAddWithoutValidation(k, v);
        return req;
    }

    private static async Task<IDocument> GetDocumentAsync(HttpClient session, string url)
    {
        using var resp = await session.SendAsync(NewRequest(HttpMethod.Get, url));
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        return new HtmlParser().ParseDocument(new System.Text.UTF8Encoding(false, false).GetString(bytes));
    }

    private static IElement? PickForm(IDocument doc, string? submitLabel)
    {
        var forms = doc.QuerySelectorAll("form").ToList();
        if (submitLabel is not null)
        {
            foreach (var f in forms)
            {
                var hasInput = f.QuerySelectorAll("input, button").Any(e => e.GetAttribute("type") == "submit" && e.GetAttribute("value") == submitLabel);
                if (hasInput || f.QuerySelectorAll("button").Any(b => GetTextStripped(b) == submitLabel)) return f;
            }
        }
        foreach (var f in forms)
            if ((f.GetAttribute("method") ?? "get").ToLowerInvariant() == "post")
                return f;
        return forms.FirstOrDefault();
    }

    // bs4 get_text(strip=True): stripped non-empty strings joined with "".
    private static string GetTextStripped(INode el) =>
        string.Concat(el.Descendants<IText>().Select(t => Normalizer.PyStrip(t.Data)).Where(s => s.Length > 0));

    // runner.harvest_form: GET the page, return the form action and every successful control with its default value.
    public static async Task<(string Action, List<KeyValuePair<string, string>> Fields, KeyValuePair<string, string>? Submit)> HarvestFormAsync(
        HttpClient session, string baseUrl, string page, string? submitLabel)
    {
        var doc = await GetDocumentAsync(session, baseUrl + page);
        var form = PickForm(doc, submitLabel) ?? throw new InvalidOperationException($"no form on {page}");
        var action = new Uri(new Uri(baseUrl + page), string.IsNullOrEmpty(form.GetAttribute("action")) ? page : form.GetAttribute("action")!).ToString();
        var fields = new List<KeyValuePair<string, string>>();
        foreach (var el in form.QuerySelectorAll("input, select, textarea"))
        {
            var name = el.GetAttribute("name");
            if (string.IsNullOrEmpty(name) || el.HasAttribute("disabled")) continue;
            switch (el.LocalName)
            {
                case "input":
                    var t = (el.GetAttribute("type") ?? "text").ToLowerInvariant();
                    if (t is "submit" or "button" or "image" or "reset" or "file") continue;
                    if (t is "checkbox" or "radio" && !el.HasAttribute("checked")) continue;
                    fields.Add(new(name, el.GetAttribute("value") ?? (t is "checkbox" or "radio" ? "on" : "")));
                    break;
                case "select":
                    var opts = el.QuerySelectorAll("option").ToList();
                    var sel = opts.Where(o => o.HasAttribute("selected")).ToList();
                    if (sel.Count == 0) sel = opts.Take(1).ToList();
                    foreach (var o in sel) fields.Add(new(name, o.GetAttribute("value") ?? Normalizer.GetText(o)));
                    break;
                default:
                    fields.Add(new(name, Normalizer.GetText(el)));
                    break;
            }
        }
        KeyValuePair<string, string>? submit = null;
        if (submitLabel is not null)
        {
            var found = false;
            foreach (var b in form.QuerySelectorAll("input, button"))
            {
                var label = b.LocalName == "input" ? b.GetAttribute("value") : GetTextStripped(b);
                if ((string.IsNullOrEmpty(b.GetAttribute("type")) ? "submit" : b.GetAttribute("type")!).ToLowerInvariant() == "submit" && label == submitLabel)
                {
                    if (!string.IsNullOrEmpty(b.GetAttribute("name"))) submit = new(b.GetAttribute("name")!, b.GetAttribute("value") ?? "");
                    found = true;
                    break;
                }
            }
            if (!found) throw new InvalidOperationException($"submit '{submitLabel}' not found on {page}");
        }
        return (action, fields, submit);
    }

    // runner.build_post: defaults + overrides by logical name - omitted fields + submit button.
    public static async Task<(string Url, List<KeyValuePair<string, string>> Data)> BuildPostAsync(HttpClient session, string baseUrl, FlowSpec flow)
    {
        var spec = flow.Form!;
        var (action, fields, submit) = await HarvestFormAsync(session, baseUrl, (string)spec["page"]!, (string?)spec["submit"]);
        var overrides = new List<KeyValuePair<string, string>>();
        if (spec["fields"] is JsonObject fo)
            foreach (var kv in fo) overrides.Add(new(kv.Key, kv.Value is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : kv.Value?.ToJsonString() ?? ""));
        var omit = (spec["omit"] as JsonArray)?.Select(n => (string)n!).ToHashSet() ?? [];
        var output = new List<KeyValuePair<string, string>>();
        foreach (var (name, value) in fields)
        {
            var logical = Normalizer.LogicalName(name)!;
            if (omit.Contains(name) || omit.Contains(logical)) continue;
            var idx = overrides.FindIndex(o => o.Key == logical);
            var v = value;
            if (idx >= 0)
            {
                v = overrides[idx].Value;
                overrides.RemoveAt(idx);
            }
            output.Add(new(name, v));
        }
        output.AddRange(overrides);
        if (submit is not null) output.Add(submit.Value);
        if (flow.Path is not null) action = baseUrl + flow.Path;
        return (action, output);
    }

    private static string? Header(HttpResponseMessage r, string name)
    {
        if (r.Headers.TryGetValues(name, out var v) || r.Content.Headers.TryGetValues(name, out v)) return string.Join(", ", v);
        return null;
    }

    public static async Task<JsonObject> RunFlowAsync(HttpClient session, string baseUrl, FlowSpec flow)
    {
        var headers = new Dictionary<string, string>();
        if (flow.Raw["headers"] is JsonObject ho)
            foreach (var kv in ho) headers[kv.Key] = (string)kv.Value!;
        string url;
        List<KeyValuePair<string, string>>? data = null;
        var request = new JsonObject { ["method"] = flow.Method, ["path"] = flow.Path };
        if (flow.Form is not null)
        {
            (url, data) = await BuildPostAsync(session, baseUrl, flow);
            request["path"] = Normalizer.NormUrl(url, baseUrl);
            request["form"] = new JsonArray(data.Select(kv => (JsonNode)new JsonArray(kv.Key,
                Normalizer.TokenFields.Contains(Normalizer.LogicalName(kv.Key)!) || Normalizer.TokenFields.Contains(kv.Key) ? Normalizer.Mask : kv.Value)).ToArray());
        }
        else
        {
            url = baseUrl + flow.Path;
        }
        using var req = NewRequest(new HttpMethod(flow.Method), url, headers);
        if (data is not null) req.Content = new FormUrlEncodedContent(data);
        using var resp = await session.SendAsync(req);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var ct = Header(resp, "Content-Type");
        var keyHeaders = new JsonObject { ["content-type"] = Normalizer.MimeOf(ct) };
        var location = Header(resp, "Location");
        if (location is not null) keyHeaders["location"] = Normalizer.NormUrl(location, baseUrl);
        var status = (int)resp.StatusCode;
        return new JsonObject
        {
            ["id"] = flow.Id,
            ["request"] = request,
            ["response"] = new JsonObject
            {
                ["status"] = status,
                ["headers"] = keyHeaders,
                ["raw_sha256"] = Normalizer.Sha(bytes),
                ["body"] = Normalizer.NormaliseBody(bytes, ct, status, flow.Compare, baseUrl),
            },
        };
    }

    // runner.snapshot: every list row keyed by column header (+ Id from the row links) and the brand/type lookups.
    public static async Task<JsonObject> SnapshotAsync(HttpClient session, string baseUrl, JsonObject appCfg)
    {
        using var resp = await session.SendAsync(NewRequest(HttpMethod.Get, baseUrl + (string)appCfg["snapshot_items"]!));
        var doc = new HtmlParser().ParseDocument(new System.Text.UTF8Encoding(false, false).GetString(await resp.Content.ReadAsByteArrayAsync()));
        var headers = doc.QuerySelectorAll("table th").Select(th => Normalizer.NormWs(Normalizer.GetText(th, " "))).ToList();
        var items = new JsonArray();
        foreach (var tr in doc.QuerySelectorAll("table tr"))
        {
            var tds = tr.QuerySelectorAll("td").ToList();
            if (tds.Count == 0) continue;
            var row = new JsonObject();
            foreach (var (h, td) in headers.Zip(tds))
                if (h.Length > 0) row[h] = Normalizer.NormWs(Normalizer.GetText(td, " "));
            Match? m = null;
            foreach (var a in tr.QuerySelectorAll("a[href]"))
            {
                var href = a.GetAttribute("href")!;
                m = CatalogIdRe().Match(href);
                if (!m.Success) m = QueryIdRe().Match(href);
                if (m.Success) break;
            }
            row["Id"] = m is { Success: true } ? int.Parse(m.Groups[1].Value) : null;
            items.Add(row);
        }
        var snap = new JsonObject { ["status"] = (int)resp.StatusCode, ["items"] = items };
        var lookupsDoc = await GetDocumentAsync(session, baseUrl + (string)appCfg["snapshot_lookups"]!);
        var lookups = new JsonObject();
        foreach (var s in lookupsDoc.QuerySelectorAll("select"))
            lookups[Normalizer.LogicalName(s.GetAttribute("name")) ?? "null"] = new JsonArray(s.QuerySelectorAll("option")
                .Select(o => (JsonNode)new JsonArray(o.GetAttribute("value"), Normalizer.NormWs(Normalizer.GetText(o)))).ToArray());
        snap["lookups"] = lookups;
        return snap;
    }
}
