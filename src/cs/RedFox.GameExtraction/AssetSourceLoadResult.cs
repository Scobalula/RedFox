namespace RedFox.GameExtraction;

/// <summary>
/// The result of loading assets from a source.
/// </summary>
/// <typeparam name="TAsset">The asset type.</typeparam>
public sealed class AssetSourceLoadResult<TAsset>
{
    /// <summary>
    /// Gets the loaded source.
    /// </summary>
    public required IAssetSource Source { get; init; }

    /// <summary>
    /// Gets the assets discovered from the source.
    /// </summary>
    public required IReadOnlyList<TAsset> Assets { get; init; }
}