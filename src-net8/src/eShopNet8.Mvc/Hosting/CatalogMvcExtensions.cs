using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using NUglify;
using NUglify.Css;
using NUglify.JavaScript;

namespace eShopNet8.Mvc.Hosting
{
    // Owned by ticket T3 (MVC catalog UI): replaces Global.asax, FilterConfig, RouteConfig, BundleConfig and the
    // Web.config system.web settings (en-US globalization, InProc session) of eShopLegacyMVC.
    public static class CatalogMvcExtensions
    {
        public const string AntiforgeryFieldName = "__RequestVerificationToken";

        private static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

        // System.Web.Optimization bundles from App_Start/BundleConfig.cs. The bundle endpoints always minify (JsMinify /
        // CssMinify), even with compilation debug=true, which only makes Scripts.Render/Styles.Render emit per-file tags.
        private static readonly (string Path, string ContentType, string[] Files)[] Bundles =
        {
            ("/bundles/jquery", "text/javascript", new[] { "Scripts/jquery-3.3.1.js" }),
            ("/bundles/jqueryval", "text/javascript", new[] { "Scripts/jquery.validate.js", "Scripts/jquery.validate.unobtrusive.js" }),
            ("/bundles/modernizr", "text/javascript", new[] { "Scripts/modernizr-2.6.2.js", "Scripts/modernizr-2.8.3.js" }),
            ("/bundles/bootstrap", "text/javascript", new[] { "Scripts/bootstrap.js", "Scripts/respond.js" }),
            ("/Content/css", "text/css", new[] { "Content/bootstrap.css", "Content/custom.css", "Content/base.css", "Content/site.css" }),
        };

        public static IServiceCollection AddCatalogMvc(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllersWithViews(options =>
            {
                options.Filters.Add<AntiforgeryFailureAsServerErrorFilter>();
                options.Filters.Add<HandleErrorFilter>();
            })
            .AddViewOptions(options => options.ClientModelValidatorProviders.Add(new IntegerNumberClientModelValidatorProvider()));

            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.Cookie.Name = "ASP.NET_SessionId";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            services.AddAntiforgery(options =>
            {
                options.FormFieldName = AntiforgeryFieldName;
                options.Cookie.Name = AntiforgeryFieldName;
            });
            LegacyXFrameOptionsAntiforgery.Register(services);
            return services;
        }

        public static WebApplication UseCatalogMvc(this WebApplication app)
        {
            app.UseLegacySystemWebDefaults();
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(errorApp => errorApp.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return Task.CompletedTask;
                }));
            }

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(LegacyCulture),
                SupportedCultures = new[] { LegacyCulture },
                SupportedUICultures = new[] { LegacyCulture },
                RequestCultureProviders = new List<IRequestCultureProvider>(),
            });

            var contentTypes = new FileExtensionContentTypeProvider();
            contentTypes.Mappings[".js"] = "application/javascript";
            contentTypes.Mappings[".css"] = "text/css";
            contentTypes.Mappings[".ico"] = "image/x-icon";
            contentTypes.Mappings[".png"] = "image/png";
            app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

            app.UseSession();
            app.Use(async (context, next) =>
            {
                // Global.asax Session_Start: remembered per session and printed in the layout footer.
                await context.Session.LoadAsync();
                if (!context.Session.Keys.Contains("MachineName"))
                {
                    context.Session.SetString("MachineName", Environment.MachineName);
                    context.Session.SetString("SessionStartTime", DateTime.Now.ToString(LegacyCulture));
                }
                await next();
            });

            app.UseRouting();
            return app;
        }

        public static WebApplication MapCatalogMvc(this WebApplication app)
        {
            foreach (var (path, contentType, files) in Bundles)
            {
                var bundle = new CachedBundle(app.Environment.WebRootFileProvider, files, contentType);
                app.MapGet(path, (HttpContext context) =>
                {
                    // System.Web.Optimization's BundleHandler: publicly cacheable for a year.
                    context.Response.Headers.CacheControl = "public";
                    context.Response.Headers.Expires = DateTimeOffset.UtcNow.AddYears(1).ToString("R");
                    return Results.Text(bundle.Content, contentType, Encoding.UTF8);
                });
            }

            app.MapControllers();
            app.MapControllerRoute("Default", "{controller=Catalog}/{action=Index}/{id?}");
            return app;
        }

        /// <summary>
        /// Minified bundle built at startup and rebuilt when a source file changes, like the BundleResponse that
        /// System.Web.Optimization caches with a CacheDependency on its files.
        /// </summary>
        internal sealed class CachedBundle
        {
            private readonly IFileProvider files;
            private readonly string[] paths;
            private readonly string contentType;
            private volatile Snapshot current;

            public CachedBundle(IFileProvider files, string[] paths, string contentType)
            {
                this.files = files;
                this.paths = paths;
                this.contentType = contentType;
                current = Build();
            }

            public string Content
            {
                get
                {
                    var snapshot = current;
                    if (snapshot.Changed.HasChanged)
                    {
                        current = snapshot = Build();
                    }
                    return snapshot.Content;
                }
            }

            private Snapshot Build()
            {
                // Watch before reading so an edit made while minifying still invalidates this snapshot.
                var changed = new CompositeChangeToken(paths.Select(files.Watch).ToList());
                return new Snapshot(BuildBundle(files, paths, contentType), changed);
            }

            private sealed record Snapshot(string Content, IChangeToken Changed);
        }

        internal static string BuildBundle(IFileProvider files, IEnumerable<string> paths, string contentType)
        {
            var isScript = contentType == "text/javascript";
            // ScriptBundle joins files with ";" so a file lacking a trailing semicolon cannot merge with the next one.
            var source = Concatenate(files, paths, isScript ? ";\n" : "\n");
            var result = isScript
                ? Uglify.Js(source, new CodeSettings { EvalTreatment = EvalTreatment.MakeImmediateSafe, PreserveImportantComments = false })
                : Uglify.Css(source, new CssSettings { CommentMode = CssComment.None });
            if (!result.HasErrors)
            {
                return result.Code;
            }

            // JsMinify/CssMinify fall back to the unminified bundle, prefixed with the minifier errors.
            var errors = string.Join("\r\n", result.Errors.Where(e => e.IsError).Select(e => e.ToString()));
            return "/* Minification failed. Returning unminified contents.\r\n" + errors + "\r\n */\r\n" + source;
        }

        private static string Concatenate(IFileProvider files, IEnumerable<string> paths, string separator)
        {
            var sb = new StringBuilder();
            foreach (var path in paths)
            {
                using var reader = new StreamReader(files.GetFileInfo(path).CreateReadStream());
                sb.Append(reader.ReadToEnd()).Append(separator);
            }
            return sb.ToString();
        }

        /// <summary>MVC 5 throws HttpAntiforgeryException (HTTP 500); ASP.NET Core answers 400 by default.</summary>
        private sealed class AntiforgeryFailureAsServerErrorFilter : IAlwaysRunResultFilter
        {
            public void OnResultExecuting(ResultExecutingContext context)
            {
                if (context.Result is IAntiforgeryValidationFailedResult)
                {
                    context.Result = HandleErrorFilter.ErrorView(context);
                }
            }

            public void OnResultExecuted(ResultExecutedContext context)
            {
            }
        }

        /// <summary>FilterConfig's global HandleErrorAttribute: unhandled action exceptions render Shared/Error with 500.</summary>
        private sealed class HandleErrorFilter : IExceptionFilter
        {
            private readonly IWebHostEnvironment environment;

            public HandleErrorFilter(IWebHostEnvironment environment)
            {
                this.environment = environment;
            }

            public void OnException(ExceptionContext context)
            {
                if (environment.IsDevelopment() || context.ExceptionHandled)
                {
                    return;
                }
                context.Result = ErrorView(context);
                context.ExceptionHandled = true;
            }

            internal static ViewResult ErrorView(FilterContext context) => new ViewResult
            {
                ViewName = "Error",
                StatusCode = StatusCodes.Status500InternalServerError,
                ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState),
            };
        }

        /// <summary>
        /// MVC 5 emits data-val-number ("The field X must be a number.") for every numeric type; ASP.NET Core only for
        /// float/double/decimal. Adds it for the integer types so Id/Stock/Restock/Max stock keep the legacy attributes.
        /// </summary>
        private sealed class IntegerNumberClientModelValidatorProvider : IClientModelValidatorProvider
        {
            private static readonly HashSet<Type> IntegerTypes = new()
            {
                typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
            };

            public void CreateValidators(ClientValidatorProviderContext context)
            {
                if (IntegerTypes.Contains(context.ModelMetadata.UnderlyingOrModelType))
                {
                    context.Results.Add(new ClientValidatorItem { Validator = new NumberClientValidator(), IsReusable = true });
                }
            }
        }

        private sealed class NumberClientValidator : IClientModelValidator
        {
            public void AddValidation(ClientModelValidationContext context)
            {
                var message = context.ModelMetadata.ModelBindingMessageProvider.ValueMustBeANumberAccessor(context.ModelMetadata.GetDisplayName());
                context.Attributes.TryAdd("data-val", "true");
                context.Attributes.TryAdd("data-val-number", message);
            }
        }
    }
}
