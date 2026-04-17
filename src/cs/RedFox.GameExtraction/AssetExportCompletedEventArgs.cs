namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for completed asset export notifications.
/// </summary>
public sealed class AssetExportCompletedEventArgs : AssetExportEventArgs
{
    /// <summary>
    /// Gets a value indicating whether the export was skipped.
    /// </summary>
    public bool Skipped { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetExportCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="asset">The asset associated with the export.</param>
    /// <param name="source">The source that owns the asset.</param>
    /// <param name="configuration">The export configuration used for the operation.</param>
    /// <param name="relativeOutputDirectory">The relative output directory applied to the export when one is set.</param>
    /// <param name="skipped"><see langword="true"/> when the export was skipped; otherwise, <see langword="false"/>.</param>
    public AssetExportCompletedEventArgs(
        Asset asset,
        IAssetSource source,
        ExportConfiguration configuration,
        string relativeOutputDirectory,
        bool skipped)
        : base(asset, source, configuration, relativeOutputDirectory)
    {
        Skipped = skipped;
    }
}
