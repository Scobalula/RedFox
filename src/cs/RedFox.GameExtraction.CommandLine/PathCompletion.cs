namespace RedFox.GameExtraction.CommandLine;

internal static class PathCompletion
{
    private const int MaximumEntries = 200;

    public static IEnumerable<string> GetCompletions(string partial)
    {
        int separatorIndex = partial.LastIndexOfAny(['\\', '/']);
        string directoryPrefix = separatorIndex < 0 ? string.Empty : partial[..(separatorIndex + 1)];
        string namePrefix = partial[(separatorIndex + 1)..];
        string directory = directoryPrefix.Length == 0 ? Directory.GetCurrentDirectory() : directoryPrefix;

        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return [.. new DirectoryInfo(directory).EnumerateFileSystemInfos(namePrefix + "*").Take(MaximumEntries).Select(entry => CreateCandidate(directoryPrefix, entry))];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string CreateCandidate(string directoryPrefix, FileSystemInfo entry)
    {
        bool isDirectory = entry is DirectoryInfo;
        string path = directoryPrefix + entry.Name + (isDirectory ? Path.DirectorySeparatorChar : string.Empty);

        if (!path.Contains(' '))
        {
            return path;
        }

        return isDirectory ? $"\"{path}" : $"\"{path}\"";
    }
}
