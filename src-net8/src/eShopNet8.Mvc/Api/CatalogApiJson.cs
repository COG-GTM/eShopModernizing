using System.Text.Encodings.Web;
using System.Text.Json;

namespace eShopNet8.Mvc.Api
{
    /// <summary>
    /// JSON settings of the legacy Web API 2 (Json.NET defaults): property names as declared (PascalCase) and no
    /// HTML-safe escaping. Scoped to the API endpoints so the MVC UI's JSON options stay untouched.
    /// </summary>
    public static class CatalogApiJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }
}
