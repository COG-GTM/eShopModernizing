using System.Globalization;

namespace eShop.Catalog.WebForms.Hosting;

/// <summary>
/// Owned by slice S6. Port of legacy <c>Global.Session_Start</c>: the first request of a session stores the machine name
/// and the session start time; later requests leave them untouched, so the Site.Master footer is stable per session.
/// </summary>
public sealed class SessionInfoMiddleware
{
    public const string MachineNameKey = "MachineName";
    public const string SessionStartTimeKey = "SessionStartTime";

    private static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly RequestDelegate next;
    private readonly TimeProvider timeProvider;

    public SessionInfoMiddleware(RequestDelegate next, TimeProvider timeProvider)
    {
        this.next = next;
        this.timeProvider = timeProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(ObservabilityExtensions.HealthPath, StringComparison.OrdinalIgnoreCase))
        {
            await context.Session.LoadAsync(context.RequestAborted);
            if (context.Session.GetString(MachineNameKey) is null)
            {
                context.Session.SetString(MachineNameKey, Environment.MachineName);
                // Legacy stores DateTime.Now and renders it with the en-US culture from Web.config: "M/d/yyyy h:mm:ss tt".
                context.Session.SetString(SessionStartTimeKey, timeProvider.GetLocalNow().DateTime.ToString(LegacyCulture));
            }
        }

        await next(context);
    }

    /// <summary>The footer text legacy Site.Master renders: "{MachineName}, {SessionStartTime}".</summary>
    public static string FooterText(ISession session) =>
        $"{session.GetString(MachineNameKey)}, {session.GetString(SessionStartTimeKey)}";
}
