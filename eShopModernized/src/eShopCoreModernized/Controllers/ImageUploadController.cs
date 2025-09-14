using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Services;

namespace eShopCoreModernized.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageUploadController : ControllerBase
    {
        private readonly IImageService _imageService;
        private readonly ILogger<ImageUploadController> _logger;

        public ImageUploadController(IImageService imageService, ILogger<ImageUploadController> logger)
        {
            _imageService = imageService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(IFormFile file, int? catalogItemId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded");
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Invalid file type. Only JPG, PNG, and GIF files are allowed.");
                }

                var imageUrl = await _imageService.UploadTempImageAsync(file, catalogItemId);
                
                _logger.LogInformation("Image uploaded successfully: {ImageUrl}", imageUrl);
                
                return Ok(new { imageUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image");
                return StatusCode(500, "An error occurred while uploading the image");
            }
        }

        [HttpGet("default")]
        public IActionResult GetDefaultImage()
        {
            var defaultImageUrl = _imageService.UrlDefaultImage();
            return Ok(new { imageUrl = defaultImageUrl });
        }
    }
}
