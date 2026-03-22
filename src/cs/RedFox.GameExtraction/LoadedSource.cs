namespace RedFox.GameExtraction;

/// <summary>
/// Represents one loaded source and the assets it contributed.
/// </summary>
public class LoadedSource
{
    /// <summary>
    /// Gets the identifier of the underlying source managed by the game source.
    /// </summary>
    public required Guid SourceId { get; init; }

    /// <summary>
    /// Gets the display name shown in the UI.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the backing location for this source, if any.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Gets the number of assets loaded from this source.
    /// </summary>
    public int AssetCount { get; init; }

    /// <summary>
    /// Gets the assets contributed by this source.
    /// </summary>
    public required IReadOnlyList<IAssetEntry> Assets { get; init; }
}