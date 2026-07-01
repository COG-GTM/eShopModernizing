using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using eShopCoreAPI.Services;
using eShopCoreAPI.Models;
using eShopCoreAPI.ViewModels;

namespace eShopCoreAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(ICatalogService catalogService, ILogger<CatalogController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    /// <summary>Gets a paginated list of catalog items.</summary>
    /// <param name="pageSize">Number of items per page (1-100).</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    [HttpGet("items")]
    [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetCatalogItems(
        [FromQuery][Range(1, 100)] int pageSize = 10,
        [FromQuery][Range(0, int.MaxValue)] int pageIndex = 0)
    {
        var items = _catalogService.GetCatalogItemsPaginated(pageSize, pageIndex);
        return Ok(items);
    }

    /// <summary>Gets a single catalog item by id.</summary>
    [HttpGet("items/{id:int}")]
    [ProducesResponseType(typeof(CatalogItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetCatalogItem(int id)
    {
        var item = _catalogService.FindCatalogItem(id);
        if (item == null)
        {
            _logger.LogInformation("Catalog item {CatalogItemId} not found", id);
            return NotFound();
        }
        return Ok(item);
    }

    /// <summary>Gets all catalog brands.</summary>
    [HttpGet("brands")]
    [ProducesResponseType(typeof(IEnumerable<CatalogBrand>), StatusCodes.Status200OK)]
    public IActionResult GetCatalogBrands()
    {
        var brands = _catalogService.GetCatalogBrands();
        return Ok(brands);
    }

    /// <summary>Gets all catalog types.</summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(IEnumerable<CatalogType>), StatusCodes.Status200OK)]
    public IActionResult GetCatalogTypes()
    {
        var types = _catalogService.GetCatalogTypes();
        return Ok(types);
    }

    /// <summary>Creates a new catalog item.</summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(CatalogItem), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult CreateCatalogItem([FromBody] CatalogItem catalogItem)
    {
        _catalogService.CreateCatalogItem(catalogItem);
        _logger.LogInformation("Created catalog item {CatalogItemId}", catalogItem.Id);
        return CreatedAtAction(nameof(GetCatalogItem), new { id = catalogItem.Id }, catalogItem);
    }

    /// <summary>Updates an existing catalog item.</summary>
    [HttpPut("items/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult UpdateCatalogItem(int id, [FromBody] CatalogItem catalogItem)
    {
        if (id != catalogItem.Id)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Id mismatch",
                Detail = "The route id must match the catalog item id in the request body.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var existingItem = _catalogService.FindCatalogItem(id);
        if (existingItem == null)
        {
            return NotFound();
        }

        _catalogService.UpdateCatalogItem(catalogItem);
        _logger.LogInformation("Updated catalog item {CatalogItemId}", id);
        return NoContent();
    }

    /// <summary>Deletes a catalog item.</summary>
    [HttpDelete("items/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult DeleteCatalogItem(int id)
    {
        var item = _catalogService.FindCatalogItem(id);
        if (item == null)
        {
            return NotFound();
        }

        _catalogService.RemoveCatalogItem(item);
        _logger.LogInformation("Deleted catalog item {CatalogItemId}", id);
        return NoContent();
    }
}
