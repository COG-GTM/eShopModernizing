using Microsoft.AspNetCore.Mvc;

namespace Timberland.Catalog.Api.Controllers;

[ApiController]
[Route("api/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    /// <summary>Health/branding endpoint for the Timberland Catalog API.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "Timberland Catalog API",
            status = "Healthy",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
