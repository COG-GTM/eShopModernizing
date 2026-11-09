using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;

namespace EShop.Storefront.WebForms.Catalog;

/// <summary>CONTRACT.md §5: an antiforgery failure is a 500 (Core's default is 400).</summary>
public sealed class AntiforgeryFailureAs500Filter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is IAntiforgeryValidationFailedResult)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
