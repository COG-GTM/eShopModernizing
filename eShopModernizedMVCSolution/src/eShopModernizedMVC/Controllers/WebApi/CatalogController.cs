using eShopModernizedMVC.Services;
using System.Web.Http;

namespace eShopModernizedMVC.Controllers.WebApi
{
    public class CatalogController : ApiController
    {
        private readonly ICatalogService _service;

        public CatalogController(ICatalogService service)
        {
            _service = service;
        }

        public IHttpActionResult Get(int id)
        {
            var catalogItem = _service.FindCatalogItem(id);
            if (catalogItem == null)
            {
                return NotFound();
            }
            return Ok(catalogItem);
        }
    }
}
