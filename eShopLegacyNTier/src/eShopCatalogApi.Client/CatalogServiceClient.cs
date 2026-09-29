using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using eShopCatalog.Contracts;

namespace eShopCatalogApi.Client
{
    /// <summary>
    /// HTTP replacement for the WCF-generated <c>CatalogServiceClient</c> proxy.
    /// </summary>
    public class CatalogServiceClient : ICatalogService, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null,
        };

        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;

        /// <param name="baseAddress">Root of the catalog API, e.g. <c>http://localhost:62314/</c>.</param>
        public CatalogServiceClient(Uri baseAddress)
            : this(new HttpClient { BaseAddress = EnsureTrailingSlash(baseAddress) }, disposeHttpClient: true)
        {
        }

        /// <param name="httpClient">A client whose <see cref="HttpClient.BaseAddress"/> points at the catalog API root.</param>
        public CatalogServiceClient(HttpClient httpClient)
            : this(httpClient, disposeHttpClient: false)
        {
        }

        private CatalogServiceClient(HttpClient httpClient, bool disposeHttpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (_httpClient.BaseAddress == null)
            {
                throw new ArgumentException("HttpClient.BaseAddress must be set.", nameof(httpClient));
            }

            _disposeHttpClient = disposeHttpClient;
        }

        public CatalogItem FindCatalogItem(int id) => Run(FindCatalogItemAsync(id));

        public Task<CatalogItem> FindCatalogItemAsync(int id) =>
            GetOrDefaultAsync<CatalogItem>(ItemUri(id));

        public CatalogBrand[] GetCatalogBrands() => Run(GetCatalogBrandsAsync());

        public Task<CatalogBrand[]> GetCatalogBrandsAsync() =>
            GetAsync<CatalogBrand[]>(CatalogRoutes.Brands);

        public CatalogItem[] GetCatalogItems(int? brandIdFilter, int? typeIdFilter) =>
            Run(GetCatalogItemsAsync(brandIdFilter, typeIdFilter));

        public Task<CatalogItem[]> GetCatalogItemsAsync(int? brandIdFilter, int? typeIdFilter) =>
            GetAsync<CatalogItem[]>(string.Format(
                CultureInfo.InvariantCulture,
                "{0}?brandIdFilter={1}&typeIdFilter={2}",
                CatalogRoutes.Items,
                brandIdFilter ?? 0,
                typeIdFilter ?? 0));

        public CatalogType[] GetCatalogTypes() => Run(GetCatalogTypesAsync());

        public Task<CatalogType[]> GetCatalogTypesAsync() =>
            GetAsync<CatalogType[]>(CatalogRoutes.Types);

        public int GetAvailableStock(DateTime date, int catalogItemId) =>
            Run(GetAvailableStockAsync(date, catalogItemId));

        public Task<int> GetAvailableStockAsync(DateTime date, int catalogItemId) =>
            GetAsync<int>(string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/stock?date={2}",
                CatalogRoutes.Items,
                catalogItemId,
                FormatDate(date)));

        public void CreateAvailableStock(CatalogItemsStock catalogItemsStock) =>
            Run(CreateAvailableStockAsync(catalogItemsStock));

        public Task CreateAvailableStockAsync(CatalogItemsStock catalogItemsStock)
        {
            if (catalogItemsStock == null)
            {
                throw new ArgumentNullException(nameof(catalogItemsStock));
            }

            // Send the date without a UTC offset so the server sees the same calendar day the user picked.
            var body = new CatalogItemsStock
            {
                StockId = catalogItemsStock.StockId,
                CatalogItemId = catalogItemsStock.CatalogItemId,
                AvailableStock = catalogItemsStock.AvailableStock,
                Date = DateTime.SpecifyKind(catalogItemsStock.Date.Date, DateTimeKind.Unspecified),
            };

            return SendAsync(HttpMethod.Post, CatalogRoutes.Stock, body);
        }

        public void CreateCatalogItem(CatalogItem catalogItem) => Run(CreateCatalogItemAsync(catalogItem));

        public async Task CreateCatalogItemAsync(CatalogItem catalogItem)
        {
            if (catalogItem == null)
            {
                throw new ArgumentNullException(nameof(catalogItem));
            }

            using (var response = await _httpClient.PostAsJsonAsync(CatalogRoutes.Items, catalogItem, JsonOptions).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);
                var created = await response.Content.ReadFromJsonAsync<CatalogItem>(JsonOptions).ConfigureAwait(false);
                if (created != null)
                {
                    catalogItem.Id = created.Id;
                }
            }
        }

        public void UpdateCatalogItem(CatalogItem catalogItem) => Run(UpdateCatalogItemAsync(catalogItem));

        public Task UpdateCatalogItemAsync(CatalogItem catalogItem)
        {
            if (catalogItem == null)
            {
                throw new ArgumentNullException(nameof(catalogItem));
            }

            return SendAsync(HttpMethod.Put, ItemUri(catalogItem.Id), catalogItem);
        }

        public void RemoveCatalogItem(CatalogItem catalogItem) => Run(RemoveCatalogItemAsync(catalogItem));

        public Task RemoveCatalogItemAsync(CatalogItem catalogItem)
        {
            if (catalogItem == null)
            {
                throw new ArgumentNullException(nameof(catalogItem));
            }

            return SendAsync<object>(HttpMethod.Delete, ItemUri(catalogItem.Id), null);
        }

        public DiscountItem GetDiscount(DateTime day) => Run(GetDiscountAsync(day));

        public Task<DiscountItem> GetDiscountAsync(DateTime day) =>
            GetOrDefaultAsync<DiscountItem>(CatalogRoutes.Discount + "?day=" + FormatDate(day));

        public void Dispose()
        {
            if (_disposeHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private static string ItemUri(int id) =>
            CatalogRoutes.Items + "/" + id.ToString(CultureInfo.InvariantCulture);

        private static string FormatDate(DateTime date) =>
            date.ToString(CatalogRoutes.DateFormat, CultureInfo.InvariantCulture);

        private async Task<T> GetAsync<T>(string uri)
        {
            using (var response = await _httpClient.GetAsync(uri).ConfigureAwait(false))
            {
                await EnsureSuccessAsync(response).ConfigureAwait(false);
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions).ConfigureAwait(false);
            }
        }

        /// <summary>Maps 404 to <c>null</c>, matching the WCF operations that returned null when nothing was found.</summary>
        private async Task<T> GetOrDefaultAsync<T>(string uri) where T : class
        {
            using (var response = await _httpClient.GetAsync(uri).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                await EnsureSuccessAsync(response).ConfigureAwait(false);
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions).ConfigureAwait(false);
            }
        }

        private async Task SendAsync<T>(HttpMethod method, string uri, T body)
        {
            using (var request = new HttpRequestMessage(method, uri))
            {
                if (body != null)
                {
                    request.Content = JsonContent.Create(body, options: JsonOptions);
                }

                using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
                {
                    await EnsureSuccessAsync(response).ConfigureAwait(false);
                }
            }
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = response.Content == null
                ? null
                : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new CatalogServiceException(response.StatusCode, body);
        }

        private static Uri EnsureTrailingSlash(Uri baseAddress)
        {
            if (baseAddress == null)
            {
                throw new ArgumentNullException(nameof(baseAddress));
            }

            var value = baseAddress.ToString();
            return value.EndsWith("/", StringComparison.Ordinal) ? baseAddress : new Uri(value + "/");
        }

        // All awaits above use ConfigureAwait(false), so blocking here cannot deadlock a UI SynchronizationContext.
        private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private static void Run(Task task) => task.GetAwaiter().GetResult();
    }
}
