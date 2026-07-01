using Autofac;
using eShopLegacyMVC.Models;
using eShopLegacyMVC.Models.Infrastructure;
using eShopLegacyMVC.Modules;
using log4net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace eShopLegacyMVC
{
    public class Startup
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Startup));

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddDistributedMemoryCache();
            services.AddSession();
            services.AddApplicationInsightsTelemetry();
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            bool useMockData = Configuration.GetValue<bool>("UseMockData");
            bool useCustomizationData = Configuration.GetValue<bool>("UseCustomizationData");
            string connectionString = Configuration.GetConnectionString("CatalogDBContext");

            builder.RegisterModule(new ApplicationModule(
                useMockData,
                useCustomizationData,
                connectionString,
                Environment.ContentRootPath,
                Environment.WebRootPath));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            ConfigureLogging();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Catalog/Index");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();

            app.Use(async (context, next) =>
            {
                if (string.IsNullOrEmpty(context.Session.GetString("MachineName")))
                {
                    context.Session.SetString("MachineName", System.Environment.MachineName);
                    context.Session.SetString("SessionStartTime", DateTime.Now.ToString());
                }

                LogicalThreadContext.Properties["activityid"] = new ActivityIdHelper();
                LogicalThreadContext.Properties["requestinfo"] = new WebRequestInfo(context);

                _log.Debug("WebApplication_BeginRequest");

                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Catalog}/{action=Index}/{id?}");
            });

            ConfigDataBase(app);
        }

        private void ConfigureLogging()
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            var configFile = new FileInfo(Path.Combine(Environment.ContentRootPath, "log4Net.xml"));
            if (configFile.Exists)
            {
                log4net.Config.XmlConfigurator.Configure(logRepository, configFile);
            }
        }

        private void ConfigDataBase(IApplicationBuilder app)
        {
            var mockData = Configuration.GetValue<bool>("UseMockData");

            if (!mockData)
            {
                var initializer = app.ApplicationServices.GetRequiredService<CatalogDBInitializer>();
                Database.SetInitializer(initializer);
            }
        }
    }

    public class ActivityIdHelper
    {
        public override string ToString()
        {
            if (Trace.CorrelationManager.ActivityId == Guid.Empty)
            {
                Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            }

            return Trace.CorrelationManager.ActivityId.ToString();
        }
    }

    public class WebRequestInfo
    {
        private readonly string _info;

        public WebRequestInfo(HttpContext context)
        {
            _info = context?.Request?.Path.Value + context?.Request?.QueryString.Value
                + ", " + context?.Request?.Headers["User-Agent"];
        }

        public override string ToString()
        {
            return _info;
        }
    }
}
