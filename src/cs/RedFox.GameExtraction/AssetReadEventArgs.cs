namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for asset read notifications.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AssetReadEventArgs"/> class.
/// </remarks>
/// <param name="asset">The asset being read.</param>
/// <param name="source">The source that owns the asset.</param>
/// <param name="mode">The mode used for the read.</param>
public class AssetReadEventArgs(Asset asset, IAssetSource source, AssetReadMode mode) : AssetOperationEventArgs(asset, source)
{
    /// <summary>
    /// Gets the read mode used for the operation.
    /// </summary>
    public AssetReadMode Mode { get; } = mode;
}
