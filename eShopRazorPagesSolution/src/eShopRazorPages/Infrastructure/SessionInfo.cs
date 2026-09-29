using System.Globalization;

namespace eShopRazorPages.Infrastructure;

/// <summary>
/// Replacement for the legacy <c>Global.Session_Start</c> handler that stored the machine name and session
/// start time, rendered in the site footer by <c>Site.Master</c>.
/// </summary>
public static class SessionInfo
{
    public const string MachineNameKey = "MachineName";
    public const string SessionStartTimeKey = "SessionStartTime";

    public static IApplicationBuilder UseLegacySessionInfo(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var session = context.Session;
            if (session.GetString(MachineNameKey) is null)
            {
                session.SetString(MachineNameKey, Environment.MachineName);
                session.SetString(SessionStartTimeKey, DateTime.Now.ToString(CultureInfo.CurrentCulture));
            }

            await next();
        });
    }

    public static string Describe(ISession session) =>
        $"{session.GetString(MachineNameKey)}, {session.GetString(SessionStartTimeKey)}";
}
