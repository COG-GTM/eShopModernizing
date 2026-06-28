using Microsoft.AspNetCore.Mvc;
using Vans.Catalog.Api.Models;
using Vans.Catalog.Api.Services;
using Vans.Catalog.Api.ViewModel;

namespace Vans.Catalog.Api.Controllers;

[ApiController]
[Route("api/catalog")]
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

    /// <summary>Returns a paginated list of catalog items.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), StatusCodes.Status200OK)]
    public ActionResult<PaginatedItemsViewModel<CatalogItem>> GetItems([FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0)
    {
        _log.LogInformation("Loading catalog items pageSize={PageSize} pageIndex={PageIndex}", pageSize, pageIndex);
        if (pageSize <= 0 || pageIndex < 0)
        {
            return BadRequest("pageSize must be greater than 0 and pageIndex must be 0 or greater.");
        }

        return Ok(_service.GetCatalogItemsPaginated(pageSize, pageIndex));
    }

    /// <summary>Returns a single catalog item by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CatalogItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CatalogItem> GetById(int id)
    {
        _log.LogInformation("Loading catalog item id={Id}", id);
        var item = _service.FindCatalogItem(id);
        if (item == null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    /// <summary>Creates a new catalog item.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CatalogItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<CatalogItem> Create([FromBody] CatalogItem catalogItem)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _log.LogInformation("Creating catalog item name={Name}", catalogItem.Name);
        var created = _service.CreateCatalogItem(catalogItem);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing catalog item.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(int id, [FromBody] CatalogItem catalogItem)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (id != catalogItem.Id)
        {
            return BadRequest("Route id does not match body id.");
        }

        if (_service.FindCatalogItem(id) == null)
        {
            return NotFound();
        }

        _log.LogInformation("Updating catalog item id={Id}", id);
        _service.UpdateCatalogItem(catalogItem);
        return NoContent();
    }

    /// <summary>Deletes a catalog item by id.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        var item = _service.FindCatalogItem(id);
        if (item == null)
        {
            return NotFound();
        }

        _log.LogInformation("Deleting catalog item id={Id}", id);
        _service.RemoveCatalogItem(item);
        return NoContent();
    }

    /// <summary>Returns all catalog brands.</summary>
    [HttpGet("brands")]
    [ProducesResponseType(typeof(IEnumerable<CatalogBrand>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<CatalogBrand>> GetBrands() => Ok(_service.GetCatalogBrands());

    /// <summary>Returns all catalog types.</summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(IEnumerable<CatalogType>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<CatalogType>> GetTypes() => Ok(_service.GetCatalogTypes());
}
