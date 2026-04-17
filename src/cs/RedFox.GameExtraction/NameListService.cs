namespace RedFox.GameExtraction;

/// <summary>
/// Loads and aggregates name list files into a single <see cref="NameList"/>.
/// </summary>
public sealed class NameListService
{
    /// <summary>
    /// Gets the collection of names associated with this instance.
    /// </summary>
    public NameList List { get; } = new();

    /// <summary>
    /// Creates a <see cref="NameListService"/> by loading every file from the supplied directory.
    /// </summary>
    /// <param name="directory">The directory containing name list files.</param>
    /// <returns>A populated <see cref="NameListService"/> instance.</returns>
    public static NameListService CreateFromDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        NameListService nameListService = new();

        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                nameListService.List.LoadFromFile(file);
            }
        }

        return nameListService;
    }
}
