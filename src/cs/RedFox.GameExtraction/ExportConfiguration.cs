namespace RedFox.GameExtraction;

/// <summary>
/// Controls how assets are exported to disk.
/// </summary>
public sealed class ExportConfiguration
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOptions =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, object?> _options = EmptyOptions;

    /// <summary>
    /// Gets the root directory used for exported output.
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether existing files may be overwritten.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Gets a value indicating whether referenced assets should be exported recursively.
    /// </summary>
    public bool ExportReferences { get; init; }

    /// <summary>
    /// Gets a value indicating whether asset directory structure should be preserved under the output root.
    /// </summary>
    public bool PreserveDirectoryStructure { get; init; } = true;

    /// <summary>
    /// Gets arbitrary export options that handlers can interpret as needed.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Options
    {
        get => _options;
        init => _options = value is null
            ? EmptyOptions
            : new Dictionary<string, object?>(value, StringComparer.OrdinalIgnoreCase);
    }
}
