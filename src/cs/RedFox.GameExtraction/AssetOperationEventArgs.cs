namespace RedFox.GameExtraction;

/// <summary>
/// Provides base event data for asset operation notifications.
/// </summary>
public class AssetOperationEventArgs : EventArgs
{
    /// <summary>
    /// Gets the asset associated with the event.
    /// </summary>
    public Asset Asset { get; }

    /// <summary>
    /// Gets the source that owns the asset.
    /// </summary>
    public IAssetSource Source { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetOperationEventArgs"/> class.
    /// </summary>
    /// <param name="asset">The asset associated with the event.</param>
    /// <param name="source">The source that owns the asset.</param>
    public AssetOperationEventArgs(Asset asset, IAssetSource source)
    {
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
}
