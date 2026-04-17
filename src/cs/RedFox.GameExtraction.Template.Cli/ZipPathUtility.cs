namespace RedFox.GameExtraction.Template.Cli;

internal static class ZipPathUtility
{
    public static string GetAssetType(string name)
    {
        string extension = Path.GetExtension(name);
        return string.IsNullOrWhiteSpace(extension)
            ? "Binary"
            : extension.TrimStart('.').ToUpperInvariant();
    }

    public static string Normalize(string path)
    {
        string[] parts = path.Split(
            ['\\', '/'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(Path.DirectorySeparatorChar, parts);
    }
}
