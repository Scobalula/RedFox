namespace RedFox.GameExtraction;

/// <summary>
/// Represents the result of reading an asset.
/// </summary>
public abstract class AssetReadResult
{
    private static readonly IReadOnlyList<AssetExportReference> EmptyReferences = [];

    /// <summary>
    /// Gets the asset associated with the read.
    /// </summary>
    public required Asset Asset { get; init; }

    /// <summary>
    /// Gets referenced assets discovered while reading the asset.
    /// </summary>
    public IReadOnlyList<AssetExportReference> References { get; init; } = EmptyReferences;
}
