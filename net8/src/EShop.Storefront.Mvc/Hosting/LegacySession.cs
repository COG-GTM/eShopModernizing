using Microsoft.AspNetCore.Http.Features;

namespace EShop.Storefront.Mvc.Hosting;

/// <summary>
/// Global.asax Session_Start: the session stores the machine name and the session start time, which the layout footer
/// shows as "&lt;machine&gt;, &lt;start time&gt;". The values are written the first time the layout renders in a session, so
/// responses without the layout (static files, Web API, error pages) do not get a session cookie.
/// </summary>
public static class LegacySession
{
    public const string MachineNameKey = "MachineName";
    public const string SessionStartTimeKey = "SessionStartTime";

    public static string FooterInfo(HttpContext context)
    {
        var session = context.Features.Get<ISessionFeature>()?.Session;
        if (session is null)
        {
            return ", ";
        }
        if (session.GetString(MachineNameKey) is null)
        {
            session.SetString(MachineNameKey, Environment.MachineName);
            // DateTime.ToString() under en-US, as the legacy Session["SessionStartTime"] renders.
            session.SetString(SessionStartTimeKey, DateTime.Now.ToString("M/d/yyyy h:mm:ss tt", LegacyHosting.Culture));
        }
        return $"{session.GetString(MachineNameKey)}, {session.GetString(SessionStartTimeKey)}";
    }
}
