// Owned by T2 (MVC catalog UI).
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;

namespace EShop.Storefront.Mvc.Catalog;

/// <summary>
/// MVC 5 raised HttpAntiForgeryException (HTTP 500) when [ValidateAntiForgeryToken] failed; ASP.NET Core returns 400.
/// Turns Core's antiforgery failure result back into an unhandled exception so the response stays 500 (CONTRACT.md section 5).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LegacyAntiforgeryFailureAttribute : Attribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is IAntiforgeryValidationFailedResult)
        {
            throw new AntiforgeryValidationException(
                "The required anti-forgery form field \"__RequestVerificationToken\" is not present or invalid.");
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
