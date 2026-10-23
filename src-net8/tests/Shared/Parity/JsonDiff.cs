using System.Text.Json;
using System.Text.Json.Nodes;

namespace eShop.Parity
{
    /// <summary>Port of parity_common.projection / parity_common.diff (migration/parity/parity_common.py).</summary>
    public static class JsonDiff
    {
        public static JsonObject Projection(JsonObject flow, JsonObject normalised)
        {
            var cmp = flow["compare"] as JsonObject ?? new JsonObject();
            var status = (int)normalised["status"];
            var headers = normalised["headers"].AsObject();
            var output = new JsonObject { ["status"] = status };
            if (Flag(cmp, "mime", true) && status < 400)
            {
                output["mime"] = headers["mime"]?.DeepClone();
            }
            if (Flag(cmp, "location", true) && headers.ContainsKey("location"))
            {
                output["location"] = headers["location"]?.DeepClone();
            }
            var mode = (string)cmp["body"] ?? "auto";
            var body = normalised["body"].AsObject();
            if (mode == "none" || (mode == "auto" && (status >= 400 || (status >= 300 && status < 400))))
            {
                return output;
            }
            var kind = (string)body["kind"];
            if (mode == "sha256" || kind == "binary")
            {
                output["body"] = new JsonObject
                {
                    ["kind"] = body["kind"]?.DeepClone(),
                    ["sha256"] = body["sha256"]?.DeepClone(),
                    ["length"] = body["length"]?.DeepClone(),
                };
                return output;
            }
            if (kind == "html")
            {
                var html = body["html"].DeepClone().AsObject();
                if (!Flag(cmp, "assets", true))
                {
                    html.Remove("assets");
                }
                if (cmp["ignore_html_keys"] is JsonArray ignored)
                {
                    foreach (var key in ignored)
                    {
                        html.Remove((string)key);
                    }
                }
                output["body"] = new JsonObject { ["kind"] = "html", ["html"] = html };
                return output;
            }
            output["body"] = body.DeepClone();
            return output;
        }

        public static List<string> Diff(JsonNode expected, JsonNode actual, string path = "$", List<string> output = null, int limit = 40)
        {
            output ??= new List<string>();
            if (output.Count >= limit)
            {
                return output;
            }
            if (expected is JsonObject e && actual is JsonObject a)
            {
                foreach (var kv in e)
                {
                    if (!a.ContainsKey(kv.Key))
                    {
                        output.Add($"{path}.{kv.Key}: missing (expected {Show(kv.Value)})");
                    }
                    else
                    {
                        Diff(kv.Value, a[kv.Key], $"{path}.{kv.Key}", output, limit);
                    }
                }
                foreach (var kv in a.Where(kv => !e.ContainsKey(kv.Key)))
                {
                    output.Add($"{path}.{kv.Key}: unexpected {Show(kv.Value)}");
                }
            }
            else if (expected is JsonArray ea && actual is JsonArray aa)
            {
                if (ea.Count != aa.Count)
                {
                    output.Add($"{path}: length {aa.Count} != expected {ea.Count}");
                }
                for (var i = 0; i < Math.Min(ea.Count, aa.Count); i++)
                {
                    Diff(ea[i], aa[i], $"{path}[{i}]", output, limit);
                }
            }
            else if (!ScalarEquals(expected, actual))
            {
                output.Add($"{path}: {Show(actual)} != expected {Show(expected)}");
            }
            return output;
        }

        private static bool ScalarEquals(JsonNode expected, JsonNode actual)
        {
            if (expected == null || actual == null)
            {
                return expected == null && actual == null;
            }
            if (expected is JsonValue ev && actual is JsonValue av)
            {
                var ee = JsonDocument.Parse(ev.ToJsonString()).RootElement;
                var ae = JsonDocument.Parse(av.ToJsonString()).RootElement;
                if (ee.ValueKind == JsonValueKind.Number && ae.ValueKind == JsonValueKind.Number)
                {
                    return ee.GetDecimal() == ae.GetDecimal();
                }
                if (ee.ValueKind == JsonValueKind.String && ae.ValueKind == JsonValueKind.String)
                {
                    return ee.GetString() == ae.GetString();
                }
                return ee.ValueKind == ae.ValueKind && ee.GetRawText() == ae.GetRawText();
            }
            return false;
        }

        private static string Show(JsonNode node)
        {
            var text = node?.ToJsonString() ?? "null";
            return text.Length > 200 ? text.Substring(0, 200) : text;
        }

        private static bool Flag(JsonObject cmp, string key, bool fallback)
        {
            return cmp[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
        }
    }
}
