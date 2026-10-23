using System.Text;
using System.Text.Json.Nodes;

namespace eShop.Parity
{
    public sealed class CapturedResponse
    {
        public int Status { get; init; }
        public string Url { get; init; }
        public string ContentType { get; init; }
        public string Location { get; init; }
        public byte[] Body { get; init; }

        /// <summary>Decoded like python-requests: header charset, else ISO-8859-1 for text/*, else UTF-8.</summary>
        public string Text
        {
            get
            {
                var charset = (ContentType ?? string.Empty).Split(';').Skip(1)
                    .Select(p => p.Trim()).FirstOrDefault(p => p.StartsWith("charset=", StringComparison.OrdinalIgnoreCase))?
                    .Substring("charset=".Length).Trim('"', '\'');
                Encoding encoding;
                try
                {
                    encoding = charset != null ? Encoding.GetEncoding(charset)
                        : Normaliser.MimeOf(ContentType).StartsWith("text/") ? Encoding.Latin1 : new UTF8Encoding(false);
                }
                catch (ArgumentException)
                {
                    encoding = new UTF8Encoding(false);
                }
                return encoding.GetString(Body);
            }
        }

        public static async Task<CapturedResponse> FromAsync(HttpResponseMessage response, string url)
        {
            string Header(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, string name) =>
                headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))
                    .Select(h => string.Join(", ", h.Value)).FirstOrDefault();

            return new CapturedResponse
            {
                Status = (int)response.StatusCode,
                Url = url,
                ContentType = Header(response.Content.Headers, "Content-Type"),
                Location = Header(response.Headers, "Location"),
                Body = await response.Content.ReadAsByteArrayAsync(),
            };
        }
    }

    public sealed class FlowResult
    {
        public JsonObject Flow { get; init; }
        public CapturedResponse Response { get; init; }
        public JsonObject Normalised { get; init; }
        public JsonObject DataState { get; set; }
        public Dictionary<string, string> PostedFields { get; init; }
    }

    /// <summary>
    /// Port of parity_common.execute / replay.run_app: runs flows.json flows over one cookie session
    /// (HttpClient with cookies, no auto-redirect) and normalises the responses.
    /// </summary>
    public sealed class FlowRunner
    {
        private readonly HttpClient client;
        private readonly string baseUrl;
        private readonly Normaliser normaliser;

        public FlowRunner(HttpClient client)
        {
            this.client = client;
            baseUrl = client.BaseAddress.GetLeftPart(UriPartial.Authority);
            normaliser = new Normaliser(baseUrl);
        }

        public Normaliser Normaliser => normaliser;

        public async Task<CapturedResponse> SendAsync(HttpMethod method, string pathOrUrl, IEnumerable<KeyValuePair<string, string>> form = null,
            JsonObject headers = null)
        {
            var url = pathOrUrl.StartsWith("http", StringComparison.Ordinal) ? pathOrUrl : baseUrl + pathOrUrl;
            using var request = new HttpRequestMessage(method, url);
            if (form != null)
            {
                request.Content = new FormUrlEncodedContent(form);
            }
            if (headers != null)
            {
                foreach (var h in headers)
                {
                    request.Headers.TryAddWithoutValidation(h.Key, (string)h.Value);
                }
            }
            using var response = await client.SendAsync(request);
            return await CapturedResponse.FromAsync(response, url);
        }

        public async Task<FlowResult> ExecuteAsync(JsonObject flow, bool captureDataState = false)
        {
            var req = flow["request"].AsObject();
            var method = new HttpMethod(((string)req["method"]).ToUpperInvariant());
            var url = baseUrl + (string)req["path"];
            Dictionary<string, string> data = null;
            if (flow["form"] is JsonObject form)
            {
                var page = await SendAsync(HttpMethod.Get, (string)form["from"]);
                if (page.Status != 200)
                {
                    throw new InvalidOperationException($"form source {form["from"]} returned {page.Status}");
                }
                (var action, data) = CollectForm(page.Text, page.Url, (int?)form["index"] ?? 0);
                if (form["drop"] is JsonArray drop)
                {
                    foreach (var name in drop)
                    {
                        data.Remove((string)name);
                    }
                }
                if (form["fields"] is JsonObject fields)
                {
                    foreach (var kv in fields)
                    {
                        data[kv.Key] = (string)kv.Value;
                    }
                }
                if (!((bool?)req["path_is_explicit"] ?? false))
                {
                    url = action;
                }
            }
            var response = await SendAsync(method, url, data, req["headers"] as JsonObject);
            var result = new FlowResult
            {
                Flow = flow,
                Response = response,
                Normalised = normaliser.Response(response),
                PostedFields = data,
            };
            if (captureDataState)
            {
                result.DataState = await DataStateAsync((string)flow["app"]);
            }
            return result;
        }

        public async Task<JsonObject> DataStateAsync(string app)
        {
            return normaliser.DataState(await SendAsync(HttpMethod.Get, Baseline.DataStatePath(app)));
        }

        /// <summary>Port of parity_common._collect_form: (absolute action URL, every successful control's value).</summary>
        public static (string Action, Dictionary<string, string> Data) CollectForm(string markup, string pageUrl, int index = 0)
        {
            var doc = Normaliser.Parse(markup);
            var forms = doc.QuerySelectorAll("form")
                .Where(f => (f.GetAttribute("method") is { Length: > 0 } m ? m : "get").Equals("post", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (forms.Count == 0)
            {
                throw new InvalidOperationException($"no POST form found on {pageUrl}");
            }
            var form = forms[index];
            var data = new Dictionary<string, string>();
            foreach (var el in form.QuerySelectorAll("input, select, textarea"))
            {
                var name = el.GetAttribute("name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (el.LocalName == "input")
                {
                    var type = (el.GetAttribute("type") ?? "text").ToLowerInvariant();
                    if (type is "submit" or "button" or "image" or "reset" or "file")
                    {
                        continue;
                    }
                    if (type is "checkbox" or "radio" && !el.HasAttribute("checked"))
                    {
                        continue;
                    }
                    data[name] = el.GetAttribute("value") ?? string.Empty;
                }
                else if (el.LocalName == "select")
                {
                    var options = el.QuerySelectorAll("option");
                    var opt = options.FirstOrDefault(o => o.HasAttribute("selected")) ?? options.FirstOrDefault();
                    data[name] = opt == null ? string.Empty : opt.GetAttribute("value") ?? Normaliser.Strings(opt, string.Empty);
                }
                else
                {
                    data[name] = Normaliser.Strings(el, string.Empty);
                }
            }
            var action = form.GetAttribute("action");
            return (new Uri(new Uri(pageUrl), string.IsNullOrEmpty(action) ? pageUrl : action).ToString(), data);
        }

        /// <summary>Diffs of one flow against its legacy transcript (and data-state snapshot for snapshot flows).</summary>
        public static List<string> Compare(FlowResult result)
        {
            var flow = result.Flow;
            var id = (string)flow["id"];
            var transcript = Baseline.Transcript(id);
            var diffs = JsonDiff.Diff(JsonDiff.Projection(flow, transcript["response"].AsObject()),
                JsonDiff.Projection(flow, result.Normalised), $"{id}.response");
            if ((bool?)flow["snapshot"] ?? false)
            {
                JsonDiff.Diff(Baseline.DataState(id), result.DataState, $"{id}.data_state", diffs);
            }
            return diffs;
        }
    }
}
