using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Services;

namespace eShopCoreModernized.Controllers
{
    public class UploadImageController : Controller
    {
        private readonly IImageService _imageService;
        private readonly ILogger<UploadImageController> _logger;

        public UploadImageController(IImageService imageService, ILogger<UploadImageController> logger)
        {
            _imageService = imageService;
            _logger = logger;
        }

        [HttpPost]
        [Route("uploadimage")]
        public async Task<IActionResult> UploadImage(IFormFile HelpSectionImages, int? itemId = null)
        {
            _logger.LogInformation("Now processing... /uploadimage");

            if (HelpSectionImages == null || HelpSectionImages.Length == 0)
            {
                return BadRequest("image is not valid");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(HelpSectionImages.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest("image is not valid");
            }

            var urlImageTemp = await _imageService.UploadTempImageAsync(HelpSectionImages, itemId);
            var tempImage = new
            {
                name = new Uri(urlImageTemp).PathAndQuery,
                url = urlImageTemp
            };

            return Json(tempImage);
        }
    }
}
