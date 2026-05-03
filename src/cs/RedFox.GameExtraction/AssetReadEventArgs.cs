namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for asset read notifications.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AssetReadEventArgs"/> class.
/// </remarks>
/// <param name="asset">The asset being read.</param>
/// <param name="source">The source that owns the asset.</param>
public class AssetReadEventArgs(Asset asset, IAssetSource source) : AssetOperationEventArgs(asset, source);
