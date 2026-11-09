namespace EShop.Storefront.Mvc.Hosting;

/// <summary>
/// Global.asax Session_Start: the first request of a session stores the machine name and the session start time,
/// which the layout footer shows as "&lt;machine&gt;, &lt;start time&gt;".
/// </summary>
public sealed class LegacySessionMiddleware
{
    public const string MachineNameKey = "MachineName";
    public const string SessionStartTimeKey = "SessionStartTime";

    private readonly RequestDelegate _next;

    public LegacySessionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var session = context.Session;
        await session.LoadAsync(context.RequestAborted);
        if (session.GetString(MachineNameKey) is null)
        {
            session.SetString(MachineNameKey, Environment.MachineName);
            // DateTime.ToString() under en-US, as the legacy Session["SessionStartTime"] renders.
            session.SetString(SessionStartTimeKey, DateTime.Now.ToString("M/d/yyyy h:mm:ss tt", LegacyHosting.Culture));
        }
        await _next(context);
    }

    public static string SessionInfo(HttpContext context)
    {
        var session = context.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>()?.Session;
        return $"{session?.GetString(MachineNameKey)}, {session?.GetString(SessionStartTimeKey)}";
    }
}
