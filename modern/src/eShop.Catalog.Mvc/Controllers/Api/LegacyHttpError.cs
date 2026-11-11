namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>Web API 2 <c>HttpError</c> body: JSON <c>{"Message":..,"MessageDetail":..}</c> or XML <c>&lt;Error&gt;...&lt;/Error&gt;</c>.</summary>
public sealed record LegacyHttpError(string Message, string? MessageDetail = null)
{
    public static LegacyHttpError MethodNotAllowed(string method) =>
        new($"The requested resource does not support http method '{method}'.");

    public static LegacyHttpError NoController(string requestUri, string controllerName) =>
        new($"No HTTP resource was found that matches the request URI '{requestUri}'.",
            $"No type was found that matches the controller named '{controllerName}'.");

    /// <summary>Legacy text when an <c>int id</c> action parameter could not be bound (legacy type names kept for parity).</summary>
    public static LegacyHttpError InvalidIdParameter(string legacyActionSignature, string legacyControllerType) =>
        new("The request is invalid.",
            "The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32' " +
            $"for method '{legacyActionSignature}' in '{legacyControllerType}'. " +
            "An optional parameter must be a reference type, a nullable type, or be declared as an optional parameter.");
}
