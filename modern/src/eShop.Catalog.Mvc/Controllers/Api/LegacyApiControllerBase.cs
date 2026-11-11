using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>
/// Base for the ported Web API 2 controllers (legacy route <c>api/{controller}/{id}</c>). Deliberately not
/// <c>[ApiController]</c>: that attribute turns 404/400 into ProblemDetails bodies, while legacy returned empty 404s and
/// Web API <c>HttpError</c> bodies.
/// </summary>
public abstract class LegacyApiControllerBase : ControllerBase
{
    /// <summary>
    /// The raw <c>id</c> as Web API bound it: the query string wins over the route value (first value if repeated).
    /// Returns false when neither supplies one.
    /// </summary>
    protected bool TryGetRawId(string? routeId, out string? rawId)
    {
        if (Request.Query.TryGetValue("id", out var queryValues))
        {
            rawId = queryValues.Count > 0 ? queryValues[0] : null;
            return true;
        }

        rawId = routeId;
        return routeId is not null;
    }

    protected static LegacyWebApiResult LegacyOk(object value) => new(StatusCodes.Status200OK, value);

    protected static LegacyWebApiResult LegacyStatus(int statusCode) => new(statusCode);

    protected LegacyWebApiResult LegacyMethodNotAllowed(params string[] allow) =>
        new(StatusCodes.Status405MethodNotAllowed, LegacyHttpError.MethodNotAllowed(Request.Method), allow: allow);

    protected static LegacyWebApiResult LegacyInvalidId(string legacyActionSignature, string legacyControllerType) =>
        new(StatusCodes.Status400BadRequest, LegacyHttpError.InvalidIdParameter(legacyActionSignature, legacyControllerType));
}
