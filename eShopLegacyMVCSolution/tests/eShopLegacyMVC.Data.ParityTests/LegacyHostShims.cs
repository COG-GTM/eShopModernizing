// Minimal stand-ins for the System.Web pieces the legacy data layer touches, so it compiles on .NET 8 unchanged.
namespace System.Web
{
}

namespace System.Web.Hosting
{
    public static class HostingEnvironment
    {
        public static string ApplicationPhysicalPath { get; set; }
    }
}
