using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace eShop.Parity
{
    /// <summary>
    /// C# port of parity_common.Normaliser (migration/parity/parity_common.py): reduces a response to the
    /// host-independent structure stored in the legacy transcripts so in-process tests apply the same rules
    /// as migration/parity/replay.py (see migration/CONTRACT.md, "Parity rules").
    /// </summary>
    public sealed class Normaliser
    {
        public static readonly HashSet<string> TokenFields = new HashSet<string>
        {
            "__RequestVerificationToken", "__VIEWSTATE", "__VIEWSTATEGENERATOR", "__VIEWSTATEENCRYPTED",
            "__EVENTVALIDATION", "__EVENTTARGET", "__EVENTARGUMENT", "__LASTFOCUS", "__PREVIOUSPAGE",
        };

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex SessionInfo = new Regex(@"[^\s,]+, \d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2} [AP]M", RegexOptions.Compiled);
        private static readonly Regex Hidden = new Regex(@"display\s*:\s*none|visibility\s*:\s*hidden", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string[] HtmlTypes = { "text/html", "application/xhtml+xml" };
        private static readonly string[] JsonTypes = { "application/json", "text/json" };
        private static readonly string[] TextStaticTypes = { "text/css", "application/javascript", "text/javascript", "application/x-javascript" };
        private const string SessionPlaceholder = "<machine>, <session-start>";

        private readonly List<string> origins;

        public Normaliser(string baseUrl)
        {
            var b = new Uri(baseUrl.TrimEnd('/'));
            var set = new HashSet<string> { b.GetLeftPart(UriPartial.Authority) };
            if (b.Host is "localhost" or "127.0.0.1")
            {
                set.Add($"{b.Scheme}://localhost:{b.Port}");
                set.Add($"{b.Scheme}://127.0.0.1:{b.Port}");
            }
            origins = set.OrderByDescending(o => o.Length).ToList();
        }

        public static string Ws(string text) => Whitespace.Replace(text ?? string.Empty, " ").Trim();

        public static string MimeOf(string contentType) => (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();

        public string Url(string value, string pageUrl = null)
        {
            if (value == null)
            {
                return null;
            }
            value = value.Trim();
            if (pageUrl != null && !(value.StartsWith("http://") || value.StartsWith("https://") || value.StartsWith("#")
                                     || value.StartsWith("javascript:") || value.StartsWith("mailto:")))
            {
                value = new Uri(new Uri(pageUrl), value).ToString();
            }
            foreach (var origin in origins)
            {
                if (value.StartsWith(origin, StringComparison.Ordinal))
                {
                    value = "{base}" + value.Substring(origin.Length);
                }
            }
            return value;
        }

        public string Text(string value)
        {
            value = Ws(value);
            foreach (var origin in origins)
            {
                value = value.Replace(origin, "{base}");
            }
            return SessionInfo.Replace(value, SessionPlaceholder);
        }

        public JsonObject Response(CapturedResponse resp)
        {
            return new JsonObject
            {
                ["status"] = resp.Status,
                ["headers"] = Headers(resp),
                ["body"] = Body(resp),
            };
        }

        public JsonObject Headers(CapturedResponse resp)
        {
            var output = new JsonObject();
            if (resp.ContentType != null)
            {
                output["content-type"] = resp.ContentType;
                output["mime"] = MimeOf(resp.ContentType);
            }
            if (resp.Location != null)
            {
                output["location"] = Url(resp.Location, resp.Url);
            }
            return output;
        }

        public JsonObject Body(CapturedResponse resp)
        {
            var mime = MimeOf(resp.ContentType);
            var raw = resp.Body;
            if (raw.Length == 0)
            {
                return new JsonObject { ["kind"] = "empty" };
            }
            if (HtmlTypes.Contains(mime) && resp.Status < 400 && FirstNonSpace(raw) == '<')
            {
                return new JsonObject { ["kind"] = "html", ["html"] = Html(resp.Text, resp.Url) };
            }
            if (JsonTypes.Contains(mime))
            {
                try
                {
                    return new JsonObject { ["kind"] = "json", ["json"] = JsonNode.Parse(new UTF8Encoding(false).GetString(StripBom(raw))) };
                }
                catch (System.Text.Json.JsonException)
                {
                }
            }
            var output = new JsonObject
            {
                ["kind"] = "binary",
                ["sha256"] = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(),
                ["length"] = raw.Length,
            };
            if (!TextStaticTypes.Contains(mime) && !mime.StartsWith("image/") && raw.Length <= 4096)
            {
                output["base64"] = Convert.ToBase64String(raw);
            }
            return output;
        }

        public JsonObject Html(string markup, string pageUrl)
        {
            var doc = Parse(markup);
            var assets = new JsonObject
            {
                ["stylesheets"] = Array(doc.QuerySelectorAll("link")
                    .Where(l => (l.GetAttribute("rel") ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Contains("stylesheet"))
                    .Select(l => (JsonNode)Url(l.GetAttribute("href"), pageUrl))),
                ["scripts"] = Array(doc.QuerySelectorAll("script").Where(s => s.HasAttribute("src"))
                    .Select(s => (JsonNode)Url(s.GetAttribute("src"), pageUrl))),
            };
            foreach (var el in doc.QuerySelectorAll("script, style, noscript, template").ToList())
            {
                el.Remove();
            }
            foreach (var el in doc.QuerySelectorAll("[style]").Where(e => Hidden.IsMatch(e.GetAttribute("style"))).ToList())
            {
                el.Remove();
            }
            var titleEl = doc.QuerySelector("title");
            var body = (IParentNode)doc.Body ?? doc;
            return new JsonObject
            {
                ["title"] = titleEl == null ? null : Text(Strings(titleEl, string.Empty)),
                ["headings"] = Array(body.QuerySelectorAll("h1, h2, h3, h4")
                    .Select(h => (JsonNode)new JsonArray(h.LocalName, Text(Strings(h))))),
                ["text"] = Text(Strings((INode)body)),
                ["links"] = Array(body.QuerySelectorAll("a").Select(a => (JsonNode)new JsonObject
                {
                    ["text"] = Text(Strings(a)),
                    ["href"] = Url(a.GetAttribute("href"), pageUrl),
                    ["class"] = Array(a.ClassList.OrderBy(c => c, StringComparer.Ordinal).Select(c => (JsonNode)c)),
                })),
                ["images"] = Array(body.QuerySelectorAll("img, image").Select(i => (JsonNode)Url(i.GetAttribute("src"), pageUrl))),
                ["tables"] = Array(body.QuerySelectorAll("table").Select(t => (JsonNode)Table(t))),
                ["definitions"] = Array(body.QuerySelectorAll("dt").Select(dt =>
                {
                    var dd = FollowingSiblings(dt).FirstOrDefault(s => s.LocalName == "dd");
                    return (JsonNode)new JsonArray(Text(Strings(dt)), dd == null ? null : Text(Strings(dd)));
                })),
                ["forms"] = Array(body.QuerySelectorAll("form").Select(f => (JsonNode)Form(f, pageUrl))),
                ["validation"] = Array(body.QuerySelectorAll(".field-validation-error, .validation-summary-errors li, .text-danger")
                    .Where(e => Ws(Strings(e, string.Empty)).Length > 0)
                    .Select(e => (JsonNode)Text(Strings(e)))),
                ["assets"] = assets,
            };
        }

        public JsonObject DataState(CapturedResponse resp)
        {
            var doc = Parse(resp.Text);
            var table = doc.QuerySelectorAll("table").FirstOrDefault(t => t.ClassList.Contains("table"));
            var rows = new JsonArray();
            if (table != null)
            {
                foreach (var tr in table.QuerySelectorAll("tr"))
                {
                    if (tr.QuerySelector("th") != null)
                    {
                        continue;
                    }
                    var tds = tr.Children.Where(c => c.LocalName == "td").ToList();
                    if (tds.Count == 0)
                    {
                        continue;
                    }
                    var img = tr.QuerySelector("img, image");
                    rows.Add(new JsonObject
                    {
                        ["image"] = img == null ? null : Url(img.GetAttribute("src"), resp.Url),
                        ["cells"] = Array(tds.Select(td => (JsonNode)Text(Strings(td)))),
                        ["links"] = Array(tr.QuerySelectorAll("a").Where(a => a.HasAttribute("href"))
                            .Select(a => (JsonNode)Url(a.GetAttribute("href"), resp.Url))),
                    });
                }
            }
            var pager = doc.QuerySelectorAll(".esh-pager-item").FirstOrDefault(e => e.LocalName != "a")
                        ?? doc.QuerySelectorAll("span").FirstOrDefault(e => e.ClassList.Contains("esh-pager-item"));
            return new JsonObject
            {
                ["status"] = resp.Status,
                ["pager"] = pager == null ? null : Text(Strings(pager)),
                ["row_count"] = rows.Count,
                ["rows"] = rows,
            };
        }

        public static IDocument Parse(string markup)
        {
            var doc = new HtmlParser().ParseDocument(markup ?? string.Empty);
            foreach (var comment in doc.Descendants<IComment>().ToList())
            {
                comment.RemoveFromParent();
            }
            return doc;
        }

        /// <summary>BeautifulSoup get_text(separator): every descendant text node joined with the separator.</summary>
        public static string Strings(INode node, string separator = " ")
        {
            return string.Join(separator, node.Descendants<IText>().Select(t => t.Data));
        }

        private JsonObject Table(IElement table)
        {
            return new JsonObject
            {
                ["rows"] = Array(table.QuerySelectorAll("tr").Select(tr => (JsonNode)Array(
                    tr.Children.Where(c => c.LocalName is "th" or "td").Select(c => (JsonNode)Text(Strings(c)))))),
            };
        }

        private JsonObject Form(IElement form, string pageUrl)
        {
            var fields = new JsonArray();
            foreach (var el in form.QuerySelectorAll("input, select, textarea, button"))
            {
                var name = el.GetAttribute("name");
                if (name != null && TokenFields.Contains(name))
                {
                    continue;
                }
                var f = new JsonObject { ["tag"] = el.LocalName, ["name"] = name };
                switch (el.LocalName)
                {
                    case "input":
                        var type = (el.GetAttribute("type") ?? "text").ToLowerInvariant();
                        f["type"] = type;
                        f["value"] = el.GetAttribute("value");
                        if (type is "checkbox" or "radio")
                        {
                            f["checked"] = el.HasAttribute("checked");
                        }
                        break;
                    case "select":
                        f["options"] = Array(el.QuerySelectorAll("option").Select(o => (JsonNode)new JsonArray(
                            o.GetAttribute("value"), Text(Strings(o, string.Empty)), o.HasAttribute("selected"))));
                        break;
                    case "textarea":
                        f["value"] = Strings(el, string.Empty);
                        break;
                    default:
                        f["type"] = (el.GetAttribute("type") ?? "submit").ToLowerInvariant();
                        f["text"] = Text(Strings(el));
                        break;
                }
                if (el.HasAttribute("readonly"))
                {
                    f["readonly"] = true;
                }
                if (el.HasAttribute("disabled"))
                {
                    f["disabled"] = true;
                }
                var dataVal = el.Attributes.Where(a => a.Name.StartsWith("data-val", StringComparison.Ordinal))
                    .OrderBy(a => a.Name, StringComparer.Ordinal).ToList();
                if (dataVal.Count > 0)
                {
                    var dv = new JsonObject();
                    foreach (var a in dataVal)
                    {
                        dv[a.Name] = a.Value;
                    }
                    f["data-val"] = dv;
                }
                fields.Add(f);
            }
            var action = form.GetAttribute("action");
            return new JsonObject
            {
                ["method"] = (form.GetAttribute("method") is { Length: > 0 } m ? m : "get").ToUpperInvariant(),
                ["action"] = Url(string.IsNullOrEmpty(action) ? pageUrl : action, pageUrl),
                ["fields"] = fields,
            };
        }

        private static IEnumerable<IElement> FollowingSiblings(IElement el)
        {
            for (var s = el.NextElementSibling; s != null; s = s.NextElementSibling)
            {
                yield return s;
            }
        }

        private static JsonArray Array(IEnumerable<JsonNode> items) => new JsonArray(items.ToArray());

        private static char FirstNonSpace(byte[] raw)
        {
            foreach (var b in raw)
            {
                if (b is not ((byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or 0x0b or 0x0c))
                {
                    return (char)b;
                }
            }
            return '\0';
        }

        private static byte[] StripBom(byte[] raw)
        {
            return raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF ? raw[3..] : raw;
        }
    }
}
