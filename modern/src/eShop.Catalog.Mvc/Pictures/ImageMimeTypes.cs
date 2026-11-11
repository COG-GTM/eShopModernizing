namespace eShop.Catalog.Mvc.Pictures;

/// <summary>Legacy PicController.GetImageMimeTypeFromImageFileExtension (case-sensitive, like the legacy switch).</summary>
public static class ImageMimeTypes
{
    public const string Fallback = "application/octet-stream";

    public static string FromExtension(string? extension) => extension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".tiff" => "image/tiff",
        ".wmf" => "image/wmf",
        ".jp2" => "image/jp2",
        ".svg" => "image/svg+xml",
        _ => Fallback,
    };
}
