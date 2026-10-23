using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace eShopNet8.IntegrationTests.Parity;

// C# port of modernization/parity/normalize.py (rules in modernization/CONTRACT.md sections 3-4). Change together.
public static partial class Normalizer
{
    public const string Mask = "<masked>";

    public static readonly HashSet<string> TokenFields = new(StringComparer.Ordinal)
    {
        "__RequestVerificationToken", "__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION",
        "__EVENTTARGET", "__EVENTARGUMENT", "__PREVIOUSPAGE", "__LASTFOCUS",
    };

    private static readonly Dictionary<string, string[]> KeepAttrs = new()
    {
        ["a"] = ["href"], ["img"] = ["src", "alt"], ["image"] = ["src"], ["form"] = ["action", "method"],
        ["input"] = ["name", "type", "value", "readonly", "checked", "disabled"], ["select"] = ["name"],
        ["option"] = ["value", "selected"], ["textarea"] = ["name"], ["button"] = ["type", "name", "value"], ["label"] = ["for"],
    };

    private static readonly HashSet<string> StructTags = new(KeepAttrs.Keys.Concat(new[]
    {
        "title", "h1", "h2", "h3", "h4", "h5", "h6", "table", "th", "td", "tr", "dt", "dd", "ul", "li", "span", "small", "p",
    }));

    private static readonly HashSet<string> TextTags = new()
    {
        "title", "h1", "h2", "h3", "h4", "h5", "h6", "th", "td", "dt", "dd", "option", "label", "button", "a", "span", "small",
        "li", "p", "textarea",
    };

    private static readonly HashSet<string> BooleanAttrs = new() { "readonly", "checked", "disabled", "selected" };
    private static readonly string[] TextTypes = ["text/css", "application/javascript", "text/javascript", "application/x-javascript"];
    private const int BinaryInlineLimit = 262144;

    // Session footer "{MachineName}, {SessionStartTime}" (INVENTORY Q14).
    [GeneratedRegex(@"^\s*[^,<>]+,\s*\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2}\s*(AM|PM)?\s*$")]
    private static partial Regex SessionRe();

    [GeneratedRegex(@"^(esh-pager-item--hidden|esh-pager-item--navigable|field-validation-error|input-validation-error|validation-summary-errors|text-danger|esh-price)$")]
    private static partial Regex KeepClassRe();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WsRe();

    // HTML5 parsers rewrite `<image>` to `<img>`; lxml (the reference parser) keeps it (legacy Default.aspx emits <image>).
    [GeneratedRegex(@"<(/?)image(?=[\s/>])", RegexOptions.IgnoreCase)]
    private static partial Regex ImageTagRe();

    private const string ImageAlias = "eshop-lxml-image";

    [GeneratedRegex(@"[$.]")]
    private static partial Regex LogicalSplitRe();

    public static string? MimeOf(string? contentType)
    {
        var m = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        return m.Length == 0 ? null : m;
    }

    public static string NormWs(string? s) => PyStrip(WsRe().Replace(s ?? "", " "));

    // Python str.strip(): strips characters for which str.isspace() is true.
    public static string PyStrip(string s) => s.Trim();

    public static string? NormUrl(string? u, string? baseUrl)
    {
        if (u is null) return null;
        u = PyStrip(u);
        if (baseUrl is not null && Uri.TryCreate(u, UriKind.Absolute, out var abs) && (abs.Scheme == "http" || abs.Scheme == "https")
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out var b))
        {
            var netloc = Netloc(u);
            if (netloc.Length > 0 && abs.Scheme == b.Scheme && string.Equals(netloc, Netloc(baseUrl), StringComparison.OrdinalIgnoreCase))
            {
                // urlsplit path + "?" + query, without any re-encoding
                var rest = u[(u.IndexOf("//", StringComparison.Ordinal) + 2 + netloc.Length)..];
                var hash = rest.IndexOf('#');
                if (hash >= 0) rest = rest[..hash];
                var q = rest.IndexOf('?');
                var path = q >= 0 ? rest[..q] : rest;
                var query = q >= 0 ? rest[(q + 1)..] : "";
                u = path + (query.Length > 0 ? "?" + query : "");
            }
        }
        return u;
    }

    private static string Netloc(string url)
    {
        var i = url.IndexOf("//", StringComparison.Ordinal);
        if (i < 0) return "";
        var rest = url[(i + 2)..];
        var end = rest.IndexOfAny(['/', '?', '#']);
        return end >= 0 ? rest[..end] : rest;
    }

    public static string? LogicalName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var parts = LogicalSplitRe().Split(name);
        return parts[^1];
    }

    // bs4 `el.get_text(sep)`: every descendant text node joined with `sep`.
    public static string GetText(INode el, string sep = "") =>
        string.Join(sep, el.Descendants<IText>().Select(t => t.Data));

    public static string MaskText(string s)
    {
        s = NormWs(s);
        return SessionRe().IsMatch(s) ? Mask : s;
    }

    public static JsonObject NormaliseHtml(string text, string? baseUrl)
    {
        var doc = new HtmlParser().ParseDocument(ImageTagRe().Replace(text, "<$1" + ImageAlias));
        foreach (var el in doc.QuerySelectorAll("script, style, noscript, template, link, meta").ToList())
            el.Remove();
        foreach (var c in doc.Descendants<IComment>().ToList())
            c.RemoveFromParent();

        var structure = new JsonArray();
        foreach (var el in doc.All)
        {
            var tag = el.LocalName == ImageAlias ? "image" : el.LocalName;
            if (!StructTags.Contains(tag)) continue;
            var item = new JsonObject { ["tag"] = tag };
            if (KeepAttrs.TryGetValue(tag, out var attrs))
            {
                foreach (var a in attrs)
                {
                    if (!el.HasAttribute(a)) continue;
                    JsonNode? v = el.GetAttribute(a) ?? "";
                    var sv = (string)v!;
                    if (a == "name") v = LogicalName(sv);
                    else if (tag == "label" && a == "for") v = LogicalName(sv.Replace("_", "$"));
                    if (a is "href" or "src" or "action") v = NormUrl(sv, baseUrl);
                    if (tag == "input" && a == "value" && TokenFields.Contains(el.GetAttribute("name") ?? "")) v = Mask;
                    if (BooleanAttrs.Contains(a)) v = true;
                    item[a] = v;
                }
            }
            var cls = el.ClassList.Where(c => KeepClassRe().IsMatch(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (cls.Count > 0) item["class"] = new JsonArray(cls.Select(c => (JsonNode?)c).ToArray());
            if (TextTags.Contains(tag)) item["text"] = MaskText(GetText(el, " "));
            structure.Add(item);
        }

        INode body = (INode?)doc.Body ?? doc;
        var lines = new JsonArray();
        foreach (var t in body.Descendants<IText>())
        {
            var s = PyStrip(t.Data);
            if (s.Length == 0) continue;
            var m = MaskText(s);
            if (m.Length > 0) lines.Add(m);
        }
        var title = doc.QuerySelector("title");
        return new JsonObject
        {
            ["kind"] = "html",
            ["title"] = MaskText(title is null ? "" : GetText(title)),
            ["text"] = lines,
            ["structure"] = structure,
        };
    }

    public static JsonObject BinaryRepr(byte[] data)
    {
        var o = new JsonObject { ["kind"] = "binary", ["sha256"] = Sha(data), ["length"] = data.Length };
        if (data.Length <= BinaryInlineLimit) o["base64"] = Convert.ToBase64String(data);
        return o;
    }

    public static string Sha(byte[]? data) => Convert.ToHexString(SHA256.HashData(data ?? [])).ToLowerInvariant();

    // Minimal MS-NRBF reader: BinaryObjectString values in stream order (legacy /api/files, never deserialised).
    public static List<string> NrbfStrings(byte[] data)
    {
        var output = new List<string>();
        var i = 0;
        while (i < data.Length)
        {
            if (data[i] == 0x06 && i + 5 < data.Length)
            {
                var j = i + 5;
                int length = 0, shift = 0;
                while (j < data.Length)
                {
                    var b = data[j];
                    length |= (b & 0x7F) << shift;
                    j++;
                    if ((b & 0x80) == 0) break;
                    shift += 7;
                }
                var take = Math.Max(0, Math.Min(length, data.Length - j));
                var chunk = data.AsSpan(j, take).ToArray();
                try
                {
                    var s = new UTF8Encoding(false, true).GetString(chunk);
                    if (length > 0 && chunk.Length == length && IsPrintable(s))
                    {
                        output.Add(s);
                        i = j + length;
                        continue;
                    }
                }
                catch (DecoderFallbackException)
                {
                }
            }
            i++;
        }
        return output;
    }

    private static bool IsPrintable(string s)
    {
        foreach (var r in s.EnumerateRunes())
        {
            if (r.Value == ' ') continue;
            var cat = Rune.GetUnicodeCategory(r);
            if (cat is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.SpaceSeparator)
                return false;
        }
        return true;
    }

    public static JsonNode? SemanticBrandList(byte[] bytes, string? contentType)
    {
        var mime = MimeOf(contentType) ?? "";
        if (mime.Contains("json"))
        {
            try
            {
                var doc = JsonNode.Parse(DecodeUtf8Sig(bytes, strict: true));
                var list = new JsonArray();
                foreach (var d in doc!.AsArray())
                {
                    var o = d!.AsObject();
                    list.Add(o.TryGetPropertyValue("Brand", out var v) ? v?.DeepClone() : o.TryGetPropertyValue("brand", out var w) ? w?.DeepClone() : null);
                }
                return list;
            }
            catch (Exception)
            {
                return null;
            }
        }
        return new JsonArray(NrbfStrings(bytes).Where(s => !s.StartsWith("eShopLegacy", StringComparison.Ordinal)).Select(s => (JsonNode?)s).ToArray());
    }

    private static string DecodeUtf8Sig(byte[] bytes, bool strict)
    {
        var span = bytes.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF) span = span[3..];
        return (strict ? new UTF8Encoding(false, true) : new UTF8Encoding(false, false)).GetString(span);
    }

    public static JsonObject NormaliseBody(byte[] bytes, string? contentType, int status, string compare, string? baseUrl)
    {
        var mime = MimeOf(contentType) ?? "";
        if (bytes.Length == 0) return new JsonObject { ["kind"] = "empty" };
        if (compare == "brands-semantic")
        {
            var o = new JsonObject { ["kind"] = "semantic", ["brands"] = SemanticBrandList(bytes, contentType) };
            foreach (var kv in BinaryRepr(bytes).ToList()) if (kv.Key != "kind") o[kv.Key] = kv.Value?.DeepClone();
            o["kind"] = "binary"; // python: {"kind": "semantic", **binary_repr} -> binary_repr's kind wins
            return o;
        }
        if (status >= 500 || compare == "status") return new JsonObject { ["kind"] = "opaque", ["length"] = bytes.Length };
        if (mime.Contains("html")) return NormaliseHtml(new UTF8Encoding(false, false).GetString(bytes), baseUrl);
        if (mime.Contains("json"))
        {
            try
            {
                return new JsonObject { ["kind"] = "json", ["json"] = JsonNode.Parse(DecodeUtf8Sig(bytes, strict: true)) };
            }
            catch (Exception e) when (e is JsonException or DecoderFallbackException)
            {
            }
        }
        if (mime.StartsWith("text/", StringComparison.Ordinal) || TextTypes.Contains(mime) || mime.Contains("xml"))
        {
            var text = DecodeUtf8Sig(bytes, strict: false).Replace("\r\n", "\n");
            var data = Encoding.UTF8.GetBytes(text);
            return new JsonObject { ["kind"] = "text", ["sha256"] = Sha(data), ["length"] = data.Length };
        }
        return BinaryRepr(bytes);
    }

    private static string? ContentTypeFamily(string? ct)
    {
        if (string.IsNullOrEmpty(ct)) return null;
        if (ct.Contains("html")) return "html";
        if (ct.Contains("json")) return "json";
        if (ct.StartsWith("image/", StringComparison.Ordinal)) return ct;
        if (ct.Contains("javascript")) return "javascript";
        return ct;
    }

    private static string? Str(JsonNode? n, string key) => n?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    // normalize.compare: (verdict, reasons) for a recorded response vs a replayed one.
    public static (string Verdict, List<string> Reasons) Compare(JsonNode expected, JsonNode actual, string compareMode)
    {
        var reasons = new List<string>();
        var es = expected["status"]!.GetValue<int>();
        var @as = actual["status"]!.GetValue<int>();
        if (es != @as) reasons.Add($"status {es} != {@as}");
        var eh = expected["headers"];
        var ah = actual["headers"];
        if (Str(eh, "location") != Str(ah, "location")) reasons.Add($"location '{Str(eh, "location")}' != '{Str(ah, "location")}'");
        var ect = Str(eh, "content-type");
        var act = Str(ah, "content-type");
        var eb = expected["body"]!;
        var ab = actual["body"]!;
        var ek = Str(eb, "kind");
        var ak = Str(ab, "kind");
        if (ek != "empty" && compareMode != "brands-semantic" && ContentTypeFamily(ect) != ContentTypeFamily(act))
            reasons.Add($"content-type '{ect}' != '{act}'");
        if (compareMode == "brands-semantic")
        {
            if (!JsonEq.Equal(eb["brands"], ab["brands"]))
                reasons.Add($"brands {eb["brands"]?.ToJsonString()} != {ab["brands"]?.ToJsonString()}");
        }
        else if (ek == "opaque" || compareMode == "status")
        {
        }
        else if (ek != ak)
        {
            reasons.Add($"body kind {ek} != {ak}");
        }
        else if (ek == "html")
        {
            foreach (var part in new[] { "title", "text", "structure" })
                if (!JsonEq.Equal(eb[part], ab[part]))
                    reasons.Add($"html {part} differs: {JsonEq.FirstDiff(eb[part], ab[part])}");
        }
        else if (ek == "json")
        {
            if (!JsonEq.Equal(eb["json"], ab["json"])) reasons.Add($"json differs: {JsonEq.FirstDiff(eb["json"], ab["json"])}");
        }
        else if (ek is "text" or "binary")
        {
            if (Str(eb, "sha256") != Str(ab, "sha256"))
                reasons.Add($"{ek} sha256 differs (len {eb["length"]} vs {ab["length"]})");
        }
        if (reasons.Count > 0) return ("different", reasons);
        if (Str(expected, "raw_sha256") == Str(actual, "raw_sha256") && ect == act) return ("identical", reasons);
        return ("equivalent-after-normalisation", reasons);
    }
}
