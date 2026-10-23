using System.Text.Json;
using System.Text.Json.Nodes;

namespace eShopNet8.IntegrationTests.Parity;

// Python `==` semantics for parsed JSON: objects compare by key set (order irrelevant), arrays by position.
public static class JsonEq
{
    public static bool Equal(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        switch (a)
        {
            case JsonObject oa when b is JsonObject ob:
                return oa.Count == ob.Count && oa.All(kv => ob.TryGetPropertyValue(kv.Key, out var v) && Equal(kv.Value, v));
            case JsonArray aa when b is JsonArray ab:
                return aa.Count == ab.Count && aa.Zip(ab).All(p => Equal(p.First, p.Second));
            case JsonValue va when b is JsonValue vb:
                return ValueEqual(va, vb);
            default:
                return false;
        }
    }

    private static bool ValueEqual(JsonValue a, JsonValue b)
    {
        var ka = Kind(a);
        var kb = Kind(b);
        if (ka != kb) return false;
        return ka switch
        {
            JsonValueKind.Number => ToDecimal(a) == ToDecimal(b),
            JsonValueKind.String => a.GetValue<string>() == b.GetValue<string>(),
            _ => true, // true/false kinds already equal
        };
    }

    private static JsonValueKind Kind(JsonValue v)
    {
        if (v.TryGetValue<JsonElement>(out var e)) return e.ValueKind;
        if (v.TryGetValue<string>(out _)) return JsonValueKind.String;
        if (v.TryGetValue<bool>(out var b)) return b ? JsonValueKind.True : JsonValueKind.False;
        return JsonValueKind.Number;
    }

    private static decimal ToDecimal(JsonValue v)
    {
        if (v.TryGetValue<JsonElement>(out var e)) return e.TryGetDecimal(out var d) ? d : (decimal)e.GetDouble();
        return Convert.ToDecimal(v.GetValue<object>(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string FirstDiff(JsonNode? a, JsonNode? b)
    {
        if (a is JsonArray aa && b is JsonArray ab)
        {
            for (var i = 0; i < Math.Min(aa.Count, ab.Count); i++)
                if (!Equal(aa[i], ab[i]))
                    return $"[{i}] {Trim(aa[i])} != {Trim(ab[i])}";
            return $"length {aa.Count} != {ab.Count}";
        }
        return $"{Trim(a)} != {Trim(b)}";
    }

    private static string Trim(JsonNode? n)
    {
        var s = n?.ToJsonString() ?? "null";
        return s.Length > 200 ? s[..200] : s;
    }
}
