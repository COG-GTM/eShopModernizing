using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace eShop.Catalog.Infrastructure.Logging;

// Puts RequestLogScopeMiddleware first in the pipeline (before error handling) without editing the frozen Program.cs.
internal sealed class RequestLogScopeStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<RequestLogScopeMiddleware>();
            next(app);
        };
    }
}
