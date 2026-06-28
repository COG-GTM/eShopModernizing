using Microsoft.AspNetCore.Mvc;
using Timberland.Catalog.Api.Models;
using Timberland.Catalog.Api.Services;
using Timberland.Catalog.Api.ViewModel;

namespace Timberland.Catalog.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Produces("application/json")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>Returns a paginated list of catalog items.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), StatusCodes.Status200OK)]
    public ActionResult<PaginatedItemsViewModel<CatalogItem>> GetItems([FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0)
    {
        if (pageSize <= 0)
        {
            return BadRequest("pageSize must be greater than zero.");
        }
        if (pageIndex < 0)
        {
            return BadRequest("pageIndex must be zero or greater.");
        }

        return Ok(_catalogService.GetCatalogItemsPaginated(pageSize, pageIndex));
    }

    /// <summary>Returns a single catalog item by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CatalogItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CatalogItem> GetItem(int id)
    {
        var item = _catalogService.FindCatalogItem(id);
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

        _catalogService.CreateCatalogItem(catalogItem);
        return CreatedAtAction(nameof(GetItem), new { id = catalogItem.Id }, catalogItem);
    }

    /// <summary>Updates an existing catalog item.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CatalogItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CatalogItem> Update(int id, [FromBody] CatalogItem catalogItem)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existing = _catalogService.FindCatalogItem(id);
        if (existing == null)
        {
            return NotFound();
        }

        catalogItem.Id = id;
        _catalogService.UpdateCatalogItem(catalogItem);
        return Ok(catalogItem);
    }

    /// <summary>Deletes a catalog item by id.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        var existing = _catalogService.FindCatalogItem(id);
        if (existing == null)
        {
            return NotFound();
        }

        _catalogService.RemoveCatalogItem(existing);
        return NoContent();
    }

    /// <summary>Returns all catalog brands.</summary>
    [HttpGet("brands")]
    [ProducesResponseType(typeof(IEnumerable<CatalogBrand>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<CatalogBrand>> GetBrands()
    {
        return Ok(_catalogService.GetCatalogBrands());
    }

    /// <summary>Returns all catalog types.</summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(IEnumerable<CatalogType>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<CatalogType>> GetTypes()
    {
        return Ok(_catalogService.GetCatalogTypes());
    }
}
