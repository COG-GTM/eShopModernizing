using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;

namespace eShopNet8.WebForms.Hosting
{
    // Razor Pages port of eShopLegacyWebForms: RouteConfig (page routes), Web.config (<globalization en-US>,
    // static MIME map), Global.asax Session_Start (footer label) and the YSOD for unhandled exceptions.
    public static class CatalogPagesExtensions
    {
        public const string MachineNameKey = "MachineName";
        public const string SessionStartTimeKey = "SessionStartTime";

        private static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

        public static IServiceCollection AddCatalogPages(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRazorPages(options =>
            {
                // RouteConfig.RegisterRoutes + the physical Default.aspx (the form action WebForms renders for "/").
                options.Conventions.AddPageRoute("/Default", "");
                options.Conventions.AddPageRoute("/Default", "Default.aspx");
                options.Conventions.AddPageRoute("/Default", "Default/index/{index}/size/{size}");
            });

            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.Cookie.Name = "ASP.NET_SessionId";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var cultures = new[] { LegacyCulture };
                options.DefaultRequestCulture = new RequestCulture(LegacyCulture, LegacyCulture);
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;
                options.RequestCultureProviders.Clear();
            });

            return services;
        }

        public static WebApplication UseCatalogPages(this WebApplication app)
        {
            app.Use(async (context, next) =>
            {
                // System.Web's default HttpCachePolicy (Cacheability.Private) for responses that set no cache policy.
                context.Response.OnStarting(() =>
                {
                    if (!context.Response.Headers.ContainsKey(Microsoft.Net.Http.Headers.HeaderNames.CacheControl))
                    {
                        context.Response.Headers.CacheControl = "private";
                    }
                    return Task.CompletedTask;
                });
                await next();
            });
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(errorApp => errorApp.Run(WriteServerErrorAsync));
            }

            app.UseRequestLocalization();
            app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = CreateLegacyContentTypeProvider() });
            app.UseSession();
            app.Use(StartLegacySessionAsync);
            app.UseRouting();
            return app;
        }

        public static WebApplication MapCatalogPages(this WebApplication app)
        {
            app.MapRazorPages();
            return app;
        }

        // IIS static MIME map recorded in the baseline (.NET 8 defaults .js to text/javascript).
        internal static FileExtensionContentTypeProvider CreateLegacyContentTypeProvider()
        {
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".js"] = "application/javascript";
            provider.Mappings[".css"] = "text/css";
            provider.Mappings[".ico"] = "image/x-icon";
            provider.Mappings[".png"] = "image/png";
            return provider;
        }

        // Global.asax Session_Start: Session["MachineName"] / Session["SessionStartTime"] (DateTime.Now, en-US "G").
        private static async Task StartLegacySessionAsync(HttpContext context, Func<Task> next)
        {
            await context.Session.LoadAsync();
            if (context.Session.GetString(MachineNameKey) == null)
            {
                context.Session.SetString(MachineNameKey, Environment.MachineName);
                context.Session.SetString(SessionStartTimeKey, DateTime.Now.ToString(LegacyCulture));
            }
            await next();
        }

        private static async Task WriteServerErrorAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                "<!DOCTYPE html><html><head><title>Runtime Error</title></head><body>" +
                "<h1>Server Error in '/' Application.</h1><h2><i>Runtime Error</i></h2></body></html>");
        }
    }
}
