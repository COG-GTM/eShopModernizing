using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace eShopWinForms.eShopServiceReference
{
    // Lightweight data contracts that mirror the JSON returned by the
    // ASP.NET Core Web API (eShopWCFService). These replace the WCF
    // service-reference generated types so the rest of the WinForms app
    // continues to compile unchanged.
    public class CatalogItem
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Picturefilename { get; set; }
        public int CatalogBrandId { get; set; }
        public int CatalogTypeId { get; set; }
        public CatalogBrand CatalogBrand { get; set; }
        public CatalogType CatalogType { get; set; }
    }

    public class CatalogBrand
    {
        public int Id { get; set; }
        public string Brand { get; set; }
    }

    public class CatalogType
    {
        public int Id { get; set; }
        public string Type { get; set; }
    }

    public class CatalogItemsStock
    {
        public int StockId { get; set; }
        public int CatalogItemId { get; set; }
        public int AvailableStock { get; set; }
        public DateTime Date { get; set; }
    }

    public class DiscountItem
    {
        public int Id { get; set; }
        public double Size { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    // Catalog operations consumed by the WinForms controller. Mirrors the
    // operations previously exposed by the WCF ICatalogService contract.
    public interface ICatalogService
    {
        IEnumerable<CatalogItem> GetCatalogItems(int brandIdFilter, int typeIdFilter);
        IEnumerable<CatalogBrand> GetCatalogBrands();
        IEnumerable<CatalogType> GetCatalogTypes();
        int GetAvailableStock(DateTime date, int catalogItemId);
        void CreateAvailableStock(CatalogItemsStock catalogItemsStock);
        DiscountItem GetDiscount(DateTime day);
    }

    /// <summary>
    /// HttpClient-based replacement for the former WCF CatalogServiceClient.
    /// Talks to the ASP.NET Core Web API (eShopWCFService) over REST/JSON.
    /// </summary>
    public class CatalogServiceClient : ICatalogService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public CatalogServiceClient()
            : this(ResolveBaseAddress())
        {
        }

        public CatalogServiceClient(string baseAddress)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseAddress)
            };
        }

        private static string ResolveBaseAddress()
        {
            string fromEnv = Environment.GetEnvironmentVariable("CATALOG_API_URL");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return fromEnv;
            }

            string fromConfig = ConfigurationManager.AppSettings["CatalogApiUrl"];
            if (!string.IsNullOrWhiteSpace(fromConfig))
            {
                return fromConfig;
            }

            return "http://localhost:62314";
        }

        public IEnumerable<CatalogItem> GetCatalogItems(int brandIdFilter, int typeIdFilter)
        {
            return GetAsync<List<CatalogItem>>(
                $"api/catalog/items?brandId={brandIdFilter}&typeId={typeIdFilter}") ?? new List<CatalogItem>();
        }

        public IEnumerable<CatalogBrand> GetCatalogBrands()
        {
            return GetAsync<List<CatalogBrand>>("api/catalog/brands") ?? new List<CatalogBrand>();
        }

        public IEnumerable<CatalogType> GetCatalogTypes()
        {
            return GetAsync<List<CatalogType>>("api/catalog/types") ?? new List<CatalogType>();
        }

        public int GetAvailableStock(DateTime date, int catalogItemId)
        {
            string dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return GetAsync<int>($"api/catalog/stock?catalogItemId={catalogItemId}&date={dateStr}");
        }

        public void CreateAvailableStock(CatalogItemsStock catalogItemsStock)
        {
            HttpResponseMessage response = _httpClient
                .PostAsJsonAsync("api/catalog/stock", catalogItemsStock)
                .GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
        }

        public DiscountItem GetDiscount(DateTime day)
        {
            string dateStr = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            HttpResponseMessage response = _httpClient
                .GetAsync($"api/catalog/discount?date={dateStr}")
                .GetAwaiter().GetResult();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<DiscountItem>(content, JsonOptions);
        }

        private T GetAsync<T>(string requestUri)
        {
            HttpResponseMessage response = _httpClient.GetAsync(requestUri).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
