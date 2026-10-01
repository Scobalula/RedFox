namespace RedFox.GameExtraction;

/// <summary>
/// Provides information about an asset that failed during a batch export.
/// </summary>
/// <param name="asset">The asset that failed to export.</param>
/// <param name="exception">The exception raised while exporting the asset.</param>
public sealed class AssetExportFailedEventArgs(Asset asset, Exception exception) : EventArgs
{
    /// <summary>
    /// Gets the asset that failed to export.
    /// </summary>
    public Asset Asset { get; } = asset ?? throw new ArgumentNullException(nameof(asset));

    /// <summary>
    /// Gets the exception raised while exporting the asset.
    /// </summary>
    public Exception Exception { get; } = exception ?? throw new ArgumentNullException(nameof(exception));
}
