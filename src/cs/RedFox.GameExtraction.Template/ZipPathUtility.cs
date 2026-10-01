namespace RedFox.GameExtraction.Template;

internal static class ZipPathUtility
{
    public static string GetAssetType(string name)
    {
        string extension = Path.GetExtension(name);
        return string.IsNullOrWhiteSpace(extension) ? "Binary" : extension.TrimStart('.').ToUpperInvariant();
    }

    public static AssetCategory GetAssetCategory(string name)
    {
        string extension = Path.GetExtension(name).ToLowerInvariant();
        return extension switch
        {
            ".fbx" or ".binfbx" or ".mesh" or ".obj" or ".gltf" or ".glb" or ".xmodel" or ".mdl" or ".model" or ".dae" or ".3ds" => AssetCategory.Model,
            ".anim" or ".animation" or ".anm" or ".xanim" or ".smd" => AssetCategory.Animation,
            ".png" or ".jpg" or ".jpeg" or ".dds" or ".tga" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".hdr" or ".exr" => AssetCategory.Image,
            ".wav" or ".mp3" or ".ogg" or ".flac" or ".wem" or ".bnk" => AssetCategory.Sound,
            ".zip" or ".pak" or ".bundle" or ".wad" or ".vpk" or ".iwd" or ".pck" => AssetCategory.Archive,
            ".txt" or ".json" or ".xml" or ".csv" or ".ini" or ".yaml" or ".yml" or ".cfg" or ".lua" => AssetCategory.Document,
            _ => AssetCategory.Other,
        };
    }

    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (Path.IsPathRooted(path))
            throw new InvalidDataException("ZIP entry paths must be relative.");

        string[] parts = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Any(part => part is "." or ".."))
            throw new InvalidDataException("ZIP entry paths cannot contain dot segments.");

        return string.Join(Path.DirectorySeparatorChar, parts);
    }
}
