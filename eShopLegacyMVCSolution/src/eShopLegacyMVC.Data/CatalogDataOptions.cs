using System;

namespace eShopLegacyMVC
{
    /// <summary>
    /// Settings that the legacy application read from Web.config appSettings and from System.Web.Hosting.
    /// </summary>
    public class CatalogDataOptions
    {
        /// <summary>Name of the connection string, matching the legacy Web.config &lt;connectionStrings&gt; entry.</summary>
        public const string ConnectionStringName = "CatalogDBContext";

        /// <summary>Legacy appSettings key "UseMockData".</summary>
        public bool UseMockData { get; set; }

        /// <summary>Legacy appSettings key "UseCustomizationData".</summary>
        public bool UseCustomizationData { get; set; }

        /// <summary>
        /// Folder that contains the Setup (CSV/zip seed files) and Pics folders. Replaces
        /// HostingEnvironment.ApplicationPhysicalPath; web hosts should set it to IWebHostEnvironment.ContentRootPath.
        /// </summary>
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    }
}
