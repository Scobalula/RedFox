namespace RedFox.GameExtraction;

/// <summary>
/// Provides per-operation context for handlers during read and write phases.
/// </summary>
public sealed class AssetOperationContext
{
    /// <summary>
    /// Gets the owning asset manager.
    /// </summary>
    public required AssetManager AssetManager { get; init; }

    /// <summary>
    /// Gets the export configuration for export operations.
    /// Null for plain read operations.
    /// </summary>
    public ExportConfiguration? ExportConfiguration { get; init; }

    /// <summary>
    /// Gets an optional explicit export directory override.
    /// </summary>
    public string? ExportDirectory { get; init; }

    /// <summary>
    /// Gets whether handlers should skip expensive reads when the output already exists.
    /// </summary>
    public bool SkipReadIfOutputExists { get; init; }

    /// <summary>
    /// Gets whether referenced assets should also be exported by handlers.
    /// </summary>
    public bool ExportReferences { get; init; }

    /// <summary>
    /// Optional handler-specific flags forwarded from <see cref="ExportConfiguration.Flags"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Flags { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Resolves the output path for an entry/extension when export configuration is available.
    /// </summary>
    public string? ResolvePath(IAssetEntry entry, string extension) =>
        ExportConfiguration?.ResolvePath(entry, extension, ExportDirectory);
}