namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for completed asset read notifications.
/// </summary>
public sealed class AssetReadCompletedEventArgs : AssetReadEventArgs
{
    /// <summary>
    /// Gets the read result produced by the operation.
    /// </summary>
    public AssetReadResult Result { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetReadCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="asset">The asset that was read.</param>
    /// <param name="source">The source that owns the asset.</param>
    /// <param name="result">The read result produced by the operation.</param>
    public AssetReadCompletedEventArgs(Asset asset, IAssetSource source, AssetReadResult result)
        : base(asset, source)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }
}
