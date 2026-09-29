extern alias ported;

using System;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Legacy = eShopLegacyMVC.Models;
using LegacyInfrastructure = eShopLegacyMVC.Models.Infrastructure;
using LegacyServices = eShopLegacyMVC.Services;
using Ported = ported::eShopLegacyMVC.Models;
using PortedInfrastructure = ported::eShopLegacyMVC.Models.Infrastructure;
using PortedOptions = ported::eShopLegacyMVC.CatalogDataOptions;
using PortedServices = ported::eShopLegacyMVC.Services;

namespace eShopLegacyMVC.Data.ParityTests
{
    /// <summary>
    /// Drives the unmodified legacy EF6 data layer the way Global.asax/Autofac did: settings come from an app config
    /// file (standing in for Web.config), HostingEnvironment.ApplicationPhysicalPath is the content root and the
    /// sequence scripts are read from AppDomain.BaseDirectory.
    /// </summary>
    internal static class LegacyHarness
    {
        // On .NET (Core) ConfigurationManager reads "<entry assembly>.dll.config", i.e. the test host's config file.
        private static readonly string ConfigFile =
            ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;

        private static void WriteConfig(bool useCustomizationData)
        {
            var connectionString = SqlServer.BaseConnectionString == null ? "" : SqlServer.LegacyConnectionString;
            File.WriteAllText(ConfigFile, $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <connectionStrings>
    <add name=""CatalogDBContext"" connectionString=""{SecurityElement.Escape(connectionString)}"" providerName=""System.Data.SqlClient"" />
  </connectionStrings>
  <appSettings>
    <add key=""UseMockData"" value=""false"" />
    <add key=""UseCustomizationData"" value=""{useCustomizationData.ToString().ToLowerInvariant()}"" />
  </appSettings>
</configuration>");
            ConfigurationManager.RefreshSection("appSettings");
            ConfigurationManager.RefreshSection("connectionStrings");
        }

        private static void CopySequenceScripts()
        {
            // The legacy initializer resolves @"Models\Infrastructure\dbo.*.Sequence.sql" against BaseDirectory. On
            // Windows that is a sub folder; on Linux the backslashes are part of the file name. Path.Combine handles both.
            foreach (var script in new[] { "dbo.catalog_hilo.Sequence.sql", "dbo.catalog_brand_hilo.Sequence.sql", "dbo.catalog_type_hilo.Sequence.sql" })
            {
                var target = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Models\Infrastructure\" + script);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(SqlServer.LegacySourceDirectory(), "Models", "Infrastructure", script), target, overwrite: true);
            }
        }

        public static void CreateAndSeed(Legacy.CatalogItemHiLoGenerator generator, bool useCustomizationData = false, string contentRoot = null)
        {
            WriteConfig(useCustomizationData);
            CopySequenceScripts();
            System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath = contentRoot ?? SqlServer.LegacySourceDirectory();
            try
            {
                Database.SetInitializer(new LegacyInfrastructure.CatalogDBInitializer(generator));
                using var context = new Legacy.CatalogDBContext();
                context.Database.Initialize(force: true);
            }
            finally
            {
                WriteConfig(useCustomizationData: false);
            }
        }

        public static void WithService(Legacy.CatalogItemHiLoGenerator generator, Action<LegacyServices.CatalogService> action)
        {
            // One context per unit of work, like InstancePerLifetimeScope per HTTP request.
            Database.SetInitializer<Legacy.CatalogDBContext>(null);
            using var service = new LegacyServices.CatalogService(new Legacy.CatalogDBContext(), generator);
            action(service);
        }
    }

    internal static class PortedHarness
    {
        public static Ported.CatalogDBContext CreateContext(string database = SqlServer.PortedDatabaseName) =>
            new Ported.CatalogDBContext(new DbContextOptionsBuilder<Ported.CatalogDBContext>()
                .UseSqlServer(SqlServer.ConnectionStringFor(database))
                .Options);

        public static bool CreateAndSeed(Ported.CatalogItemHiLoGenerator generator, bool useCustomizationData = false, string contentRoot = null, string database = SqlServer.PortedDatabaseName)
        {
            var options = Options.Create(new PortedOptions
            {
                UseCustomizationData = useCustomizationData,
                ContentRootPath = contentRoot ?? SqlServer.LegacySourceDirectory(),
            });
            using var context = CreateContext(database);
            return new PortedInfrastructure.CatalogDBInitializer(generator, options).InitializeDatabase(context);
        }

        public static void WithService(Ported.CatalogItemHiLoGenerator generator, Action<PortedServices.CatalogService> action, string database = SqlServer.PortedDatabaseName)
        {
            using var service = new PortedServices.CatalogService(CreateContext(database), generator);
            action(service);
        }
    }
}
