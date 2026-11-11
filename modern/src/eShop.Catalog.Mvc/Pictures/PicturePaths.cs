namespace eShop.Catalog.Mvc.Pictures;

/// <summary>
/// Resolves a catalog item's PictureFileName to a file under the Pics folder. Intentional hardening over legacy
/// (which did Path.Combine(Server.MapPath("~/Pics"), PictureFileName)): rooted paths, ".." segments and anything
/// whose canonical path leaves the Pics folder are rejected.
/// </summary>
public static class PicturePaths
{
    public const string FolderName = "Pics";

    public static bool TryResolve(string picsRoot, string? pictureFileName, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(pictureFileName)
            || pictureFileName.IndexOf('\0') >= 0
            || Path.IsPathRooted(pictureFileName)
            || pictureFileName.Split('/', '\\').Any(segment => segment == ".."))
        {
            return false;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(picsRoot)) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, pictureFileName));
        if (!candidate.StartsWith(root, StringComparison.Ordinal))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
