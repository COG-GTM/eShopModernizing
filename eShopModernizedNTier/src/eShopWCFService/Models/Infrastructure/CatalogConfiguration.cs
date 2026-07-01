using System;
using System.Collections.Generic;
using System.Linq;

namespace eShopWCFService.Models.Infrastructure
{
    public class CatalogConfiguration
    {
        private static readonly string defaultConnectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=eShopDatabase;Persist Security Info=True;";

        public static string ConnectionString
        {
            get
            {
                var envConnectionString = Environment.GetEnvironmentVariable("ConnectionString");
                return envConnectionString ?? defaultConnectionString;
            }
        }
    }
}