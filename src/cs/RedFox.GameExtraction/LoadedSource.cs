namespace RedFox.GameExtraction;

/// <summary>
/// Represents a successfully opened and enumerated asset source, tracking its origin
/// and providing convenient access to its entries.
/// </summary>
public sealed class LoadedSource
{
    /// <summary>The opened asset source.</summary>
    public required IAssetSource Source { get; init; }

    /// <summary>
    /// The file path from which this source was opened, or an empty string for
    /// memory-based sources.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Display name: the file name portion of <see cref="Location"/> for file sources,
    /// or <see cref="IAssetSource.Name"/> for memory sources.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(Location)
        ? Source.Name
        : Path.GetFileName(Location);

    /// <summary>All asset entries provided by this source.</summary>
    public IReadOnlyList<IAssetEntry> Assets => Source.Entries;
}
