using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eShop.Catalog.Mvc.Hosting;

/// <summary>
/// Port of legacy Global.asax Session_Start: stores the machine name and the session start time once per session,
/// rendered by the layout footer as "{MachineName}, {SessionStartTime}" (en-US DateTime.ToString()).
/// </summary>
public sealed class CatalogSessionInitializer
{
    public const string MachineNameKey = "MachineName";
    public const string SessionStartTimeKey = "SessionStartTime";

    // en-US "G" spelled out: ICU 72+ puts U+202F before AM/PM, legacy .NET Framework renders a plain space.
    private const string LegacyDateTimeFormat = "M/d/yyyy h:mm:ss tt";

    private readonly TimeProvider timeProvider;
    private readonly string machineName;

    public CatalogSessionInitializer(TimeProvider timeProvider)
        : this(timeProvider, Environment.MachineName)
    {
    }

    public CatalogSessionInitializer(TimeProvider timeProvider, string machineName)
    {
        this.timeProvider = timeProvider;
        this.machineName = machineName;
    }

    /// <summary>Sets the session values if this session has none yet; returns true when it did.</summary>
    public bool EnsureStarted(ISession session)
    {
        if (session.GetString(MachineNameKey) is not null)
        {
            return false;
        }

        session.SetString(MachineNameKey, machineName);
        session.SetString(SessionStartTimeKey, FormatSessionStartTime(timeProvider.GetLocalNow().DateTime));
        return true;
    }

    public string GetFooterText(ISession session)
    {
        EnsureStarted(session);
        return $"{session.GetString(MachineNameKey)}, {session.GetString(SessionStartTimeKey)}";
    }

    public static string FormatSessionStartTime(DateTime value)
    {
        return value.ToString(LegacyDateTimeFormat, MvcUiExtensions.LegacyCulture);
    }
}

/// <summary>Starts the session on every MVC (view) controller request, as legacy Session_Start did; API controllers are sessionless.</summary>
public sealed class CatalogSessionStartFilter : IAsyncResourceFilter
{
    private readonly CatalogSessionInitializer initializer;

    public CatalogSessionStartFilter(CatalogSessionInitializer initializer)
    {
        this.initializer = initializer;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor action
            && typeof(Controller).IsAssignableFrom(action.ControllerTypeInfo)
            && context.HttpContext.Features.Get<ISessionFeature>() is not null)
        {
            var session = context.HttpContext.Session;
            await session.LoadAsync(context.HttpContext.RequestAborted);
            initializer.EnsureStarted(session);
        }

        await next();
    }
}
