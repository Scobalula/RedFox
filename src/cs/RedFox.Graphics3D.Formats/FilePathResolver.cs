namespace RedFox.Graphics3D.Formats;

internal static class FilePathResolver
{
    public static string? ResolveReferencePath(string? path, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return string.IsNullOrWhiteSpace(baseDirectory) ? null : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }
}
