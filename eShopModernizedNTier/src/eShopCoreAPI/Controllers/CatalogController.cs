using Microsoft.AspNetCore.Mvc;
using eShopCoreAPI.Services;
using eShopCoreAPI.Models;

namespace eShopCoreAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("items")]
    public IActionResult GetCatalogItems([FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0)
    {
        var items = _catalogService.GetCatalogItemsPaginated(pageSize, pageIndex);
        return Ok(items);
    }

    [HttpGet("items/{id}")]
    public IActionResult GetCatalogItem(int id)
    {
        var item = _catalogService.FindCatalogItem(id);
        if (item == null)
        {
            return NotFound();
        }
        return Ok(item);
    }

    [HttpGet("brands")]
    public IActionResult GetCatalogBrands()
    {
        var brands = _catalogService.GetCatalogBrands();
        return Ok(brands);
    }

    [HttpGet("types")]
    public IActionResult GetCatalogTypes()
    {
        var types = _catalogService.GetCatalogTypes();
        return Ok(types);
    }

    [HttpPost("items")]
    public IActionResult CreateCatalogItem([FromBody] CatalogItem catalogItem)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _catalogService.CreateCatalogItem(catalogItem);
        return CreatedAtAction(nameof(GetCatalogItem), new { id = catalogItem.Id }, catalogItem);
    }

    [HttpPut("items/{id}")]
    public IActionResult UpdateCatalogItem(int id, [FromBody] CatalogItem catalogItem)
    {
        if (id != catalogItem.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existingItem = _catalogService.FindCatalogItem(id);
        if (existingItem == null)
        {
            return NotFound();
        }

        _catalogService.UpdateCatalogItem(catalogItem);
        return NoContent();
    }

    [HttpDelete("items/{id}")]
    public IActionResult DeleteCatalogItem(int id)
    {
        var item = _catalogService.FindCatalogItem(id);
        if (item == null)
        {
            return NotFound();
        }

        _catalogService.RemoveCatalogItem(item);
        return NoContent();
    }
}
