using eShop.Catalog.Mvc.Pictures;

namespace eShop.Catalog.Mvc.Tests.S3_MvcRead;

public class PictureFilesTests
{
    private static readonly string PicsRoot = Path.Combine(Path.GetTempPath(), "s3-pics-root", "Pics");

    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".bmp", "image/bmp")]
    [InlineData(".tiff", "image/tiff")]
    [InlineData(".wmf", "image/wmf")]
    [InlineData(".jp2", "image/jp2")]
    [InlineData(".svg", "image/svg+xml")]
    [InlineData(".webp", "application/octet-stream")]
    [InlineData(".PNG", "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    [InlineData(null, "application/octet-stream")]
    public void Mime_map_matches_legacy_switch(string? extension, string expected)
    {
        Assert.Equal(expected, ImageMimeTypes.FromExtension(extension));
    }

    [Theory]
    [InlineData("1.png")]
    [InlineData("dummy.png")]
    [InlineData("sub/7.png")]
    public void Plain_names_resolve_under_pics(string fileName)
    {
        Assert.True(PicturePaths.TryResolve(PicsRoot, fileName, out var path));
        Assert.StartsWith(Path.GetFullPath(PicsRoot) + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(Path.Combine(PicsRoot, fileName)), path);
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..\\appsettings.json")]
    [InlineData("../../../../etc/passwd")]
    [InlineData("sub/../../1.png")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("1.png\0.txt")]
    public void Traversal_rooted_and_empty_names_are_rejected(string? fileName)
    {
        Assert.False(PicturePaths.TryResolve(PicsRoot, fileName, out var path));
        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public void Sibling_folder_with_same_prefix_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "s3-prefix", "Pics");

        Assert.False(PicturePaths.TryResolve(root + Path.DirectorySeparatorChar + ".", "../PicsEvil/1.png", out _));
    }
}
