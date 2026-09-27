namespace RedFox.GameExtraction;

/// <summary>
/// Provides base event data for asset operation notifications.
/// </summary>
/// <param name="asset">The asset associated with the event.</param>
/// <param name="source">The source that owns the asset.</param>
public class AssetOperationEventArgs(Asset asset, IAssetSource source) : EventArgs
{
    /// <summary>
    /// Gets the asset associated with the event.
    /// </summary>
    public Asset Asset { get; } = asset ?? throw new ArgumentNullException(nameof(asset));

    /// <summary>
    /// Gets the source that owns the asset.
    /// </summary>
    public IAssetSource Source { get; } = source ?? throw new ArgumentNullException(nameof(source));
}
