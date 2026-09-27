namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for completed asset read notifications.
/// </summary>
/// <param name="asset">The asset that was read.</param>
/// <param name="source">The source that owns the asset.</param>
/// <param name="result">The read result produced by the operation.</param>
public sealed class AssetReadCompletedEventArgs(Asset asset, IAssetSource source, AssetReadResult result) : AssetReadEventArgs(asset, source)
{
    /// <summary>
    /// Gets the read result produced by the operation.
    /// </summary>
    public AssetReadResult Result { get; } = result ?? throw new ArgumentNullException(nameof(result));
}
