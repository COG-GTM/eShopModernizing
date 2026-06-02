using eShopWCFService.Models;
using eShopWCFService.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopWCFService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    // GET api/catalog/items?brandId=0&typeId=0
    [HttpGet("items")]
    public ActionResult<IEnumerable<CatalogItem>> GetCatalogItems([FromQuery] int brandId = 0, [FromQuery] int typeId = 0)
    {
        return Ok(_catalogService.GetCatalogItems(brandId, typeId));
    }

    // GET api/catalog/items/5
    [HttpGet("items/{id:int}")]
    public ActionResult<CatalogItem> FindCatalogItem(int id)
    {
        var item = _catalogService.FindCatalogItem(id);
        if (item == null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    // GET api/catalog/brands
    [HttpGet("brands")]
    public ActionResult<IEnumerable<CatalogBrand>> GetCatalogBrands()
    {
        return Ok(_catalogService.GetCatalogBrands());
    }

    // GET api/catalog/types
    [HttpGet("types")]
    public ActionResult<IEnumerable<CatalogType>> GetCatalogTypes()
    {
        return Ok(_catalogService.GetCatalogTypes());
    }

    // POST api/catalog/items
    [HttpPost("items")]
    public ActionResult<CatalogItem> CreateCatalogItem([FromBody] CatalogItem catalogItem)
    {
        _catalogService.CreateCatalogItem(catalogItem);
        return CreatedAtAction(nameof(FindCatalogItem), new { id = catalogItem.Id }, catalogItem);
    }

    // PUT api/catalog/items/5
    [HttpPut("items/{id:int}")]
    public IActionResult UpdateCatalogItem(int id, [FromBody] CatalogItem catalogItem)
    {
        if (id != catalogItem.Id)
        {
            return BadRequest("Route id does not match catalog item id.");
        }

        _catalogService.UpdateCatalogItem(catalogItem);
        return NoContent();
    }

    // DELETE api/catalog/items/5
    [HttpDelete("items/{id:int}")]
    public IActionResult RemoveCatalogItem(int id)
    {
        _catalogService.RemoveCatalogItem(new CatalogItem { Id = id });
        return NoContent();
    }

    // GET api/catalog/stock?catalogItemId=1&date=2017-09-20
    [HttpGet("stock")]
    public ActionResult<int> GetAvailableStock([FromQuery] int catalogItemId, [FromQuery] DateTime date)
    {
        return Ok(_catalogService.GetAvailableStock(date, catalogItemId));
    }

    // POST api/catalog/stock
    [HttpPost("stock")]
    public IActionResult CreateAvailableStock([FromBody] CatalogItemsStock catalogItemsStock)
    {
        _catalogService.CreateAvailableStock(catalogItemsStock);
        return NoContent();
    }

    // GET api/catalog/discount?date=2017-09-20
    [HttpGet("discount")]
    public ActionResult<DiscountItem> GetDiscount([FromQuery] DateTime date)
    {
        var discount = _catalogService.GetDiscount(date);
        if (discount == null)
        {
            return NotFound();
        }

        return Ok(discount);
    }
}
