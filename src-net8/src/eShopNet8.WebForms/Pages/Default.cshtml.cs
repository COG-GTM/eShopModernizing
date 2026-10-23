using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Core.ViewModel;

namespace eShopNet8.WebForms.Pages
{
    // Port of Default.aspx(.cs): ListView of the current page + Previous/Next pager.
    public class DefaultModel : LegacyPageModel
    {
        public const int DefaultPageIndex = 0;
        public const int DefaultPageSize = 10;

        private readonly ICatalogService catalogService;
        private readonly ILogger<DefaultModel> logger;

        public DefaultModel(ICatalogService catalogService, ILogger<DefaultModel> logger)
        {
            this.catalogService = catalogService;
            this.logger = logger;
        }

        public PaginatedItemsViewModel<CatalogItem> Model { get; private set; }

        public string PreviousUrl { get; private set; }
        public string PreviousCssClass { get; private set; }
        public string NextUrl { get; private set; }
        public string NextCssClass { get; private set; }

        public void OnGet() => Load();

        public void OnPost() => Load();

        private void Load()
        {
            if (RouteData.Values.ContainsKey("size") && RouteData.Values.ContainsKey("index"))
            {
                var size = RouteInt("size");
                var index = RouteInt("index");
                Model = catalogService.GetCatalogItemsPaginated(size, index);
                logger.LogInformation("Now loading... /Default.aspx?size={Size}&index={Index}", size, index);
            }
            else
            {
                Model = catalogService.GetCatalogItemsPaginated(DefaultPageSize, DefaultPageIndex);
                logger.LogInformation("Now loading... /Default.aspx?size={Size}&index={Index}", DefaultPageSize, DefaultPageIndex);
            }

            const string pagerClass = "esh-pager-item esh-pager-item--navigable";
            NextUrl = PageUrl(Model.ActualPage + 1, Model.ItemsPerPage);
            NextCssClass = pagerClass + (Model.ActualPage < Model.TotalPages - 1 ? "" : " esh-pager-item--hidden");
            PreviousUrl = PageUrl(Model.ActualPage - 1, Model.ItemsPerPage);
            PreviousCssClass = pagerClass + (Model.ActualPage > 0 ? "" : " esh-pager-item--hidden");
        }

        // GetRouteUrl("ProductsByPageRoute", new { index, size })
        private string PageUrl(int index, int size) => Url.Content($"~/Default/index/{index}/size/{size}");
    }
}
