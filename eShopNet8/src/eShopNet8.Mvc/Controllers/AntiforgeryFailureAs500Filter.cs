using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace eShopNet8.Mvc.Controllers;

// Legacy [ValidateAntiForgeryToken] threw HttpAntiForgeryException, surfacing as 500 through HandleErrorAttribute.
// ASP.NET Core answers 400 by default; the parity contract keeps the legacy status (CONTRACT.md, statuses exact).
public sealed class AntiforgeryFailureAs500Filter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is IAntiforgeryValidationFailedResult)
        {
            context.Result = new ViewResult
            {
                ViewName = "Error",
                StatusCode = StatusCodes.Status500InternalServerError,
                ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState),
            };
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
