namespace RedFox.GameExtraction.Template;

internal static class ZipPathUtility
{
    public static string GetAssetType(string name)
    {
        string extension = Path.GetExtension(name);
        return string.IsNullOrWhiteSpace(extension) ? "Binary" : extension.TrimStart('.').ToUpperInvariant();
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
