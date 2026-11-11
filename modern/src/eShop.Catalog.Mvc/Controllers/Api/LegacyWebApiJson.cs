using System.Text.Encodings.Web;
using System.Text.Json;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>
/// JSON settings matching the legacy Web API 2 JsonMediaTypeFormatter (Json.NET defaults): PascalCase member names
/// (no naming policy), compact output, and no escaping of HTML-sensitive characters such as <c>'</c>, <c>&amp;</c>, <c>&lt;</c>.
/// </summary>
public static class LegacyWebApiJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
