using RedFox.GameExtraction.Hashing;

namespace RedFox.GameExtraction;

/// <summary>
/// Loads and manages name list files.
/// </summary>
public sealed class NameListService : IDisposable
{
    /// <summary>
    /// Gets the collection of names associated with this instance.
    /// </summary>
    public NameTableManager Manager { get; } = new();

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
                nameListService.Manager.Load(file);
            }
        }

        return nameListService;
    }

    /// <summary>
    /// Opens the mapped name tables in a directory.
    /// </summary>
    public static NameListService CreateMappedFromDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var service = new NameListService();

        try
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory).Where(static file => string.Equals(Path.GetExtension(file), ".namemap", StringComparison.OrdinalIgnoreCase)))
                    service.Manager.OpenMapped(file);
            }

            return service;
        }
        catch
        {
            service.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Closes the mapped name tables owned by this service.
    /// </summary>
    public void Dispose() => Manager.Dispose();
}
