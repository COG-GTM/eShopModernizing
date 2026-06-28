using Catalog.Api.Models;
using Catalog.Api.Services;
using Catalog.Api.ViewModel;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

// Ported from eShopLegacyMVCSolution/src/eShopLegacyMVC/Controllers/CatalogController.cs.
// System.Web.Mvc (ActionResult + server-rendered Views) is replaced by an
// ASP.NET Core [ApiController] exposing REST/JSON endpoints. Constructor
// injection of ICatalogService is preserved.
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _service;
    private readonly ILogger<CatalogController> _log;

    public CatalogController(ICatalogService service, ILogger<CatalogController> log)
    {
        _service = service;
        _log = log;
    }

    // GET: /api/catalog?pageSize=10&pageIndex=0
    [HttpGet]
    public ActionResult<PaginatedItemsViewModel<CatalogItem>> Index(int pageSize = 10, int pageIndex = 0)
    {
        _log.LogInformation("Loading catalog page {PageIndex} (size {PageSize})", pageIndex, pageSize);
        return Ok(_service.GetCatalogItemsPaginated(pageSize, pageIndex));
    }

    // GET: /api/catalog/5
    [HttpGet("{id:int}")]
    public ActionResult<CatalogItem> Details(int id)
    {
        var item = _service.FindCatalogItem(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    // POST: /api/catalog
    [HttpPost]
    public ActionResult<CatalogItem> Create([FromBody] CatalogItem catalogItem)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var created = _service.CreateCatalogItem(catalogItem);
        return CreatedAtAction(nameof(Details), new { id = created.Id }, created);
    }

    // PUT: /api/catalog/5
    [HttpPut("{id:int}")]
    public IActionResult Edit(int id, [FromBody] CatalogItem catalogItem)
    {
        if (id != catalogItem.Id) return BadRequest("Route id does not match body id.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (_service.FindCatalogItem(id) == null) return NotFound();
        _service.UpdateCatalogItem(catalogItem);
        return NoContent();
    }

    // DELETE: /api/catalog/5
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var item = _service.FindCatalogItem(id);
        if (item == null) return NotFound();
        _service.RemoveCatalogItem(item);
        return NoContent();
    }

    // GET: /api/catalog/brands
    [HttpGet("brands")]
    public ActionResult<IEnumerable<CatalogBrand>> Brands() => Ok(_service.GetCatalogBrands());

    // GET: /api/catalog/types
    [HttpGet("types")]
    public ActionResult<IEnumerable<CatalogType>> Types() => Ok(_service.GetCatalogTypes());
}
