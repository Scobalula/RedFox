namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for asset export notifications.
/// </summary>
public class AssetExportEventArgs : AssetOperationEventArgs
{
    /// <summary>
    /// Gets the export configuration used for the operation.
    /// </summary>
    public ExportConfiguration Configuration { get; }

    /// <summary>
    /// Gets the relative output directory applied to the export when one is set.
    /// </summary>
    public string RelativeOutputDirectory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetExportEventArgs"/> class.
    /// </summary>
    /// <param name="asset">The asset being exported.</param>
    /// <param name="source">The source that owns the asset.</param>
    /// <param name="configuration">The export configuration used for the operation.</param>
    /// <param name="relativeOutputDirectory">The relative output directory applied to the export when one is set.</param>
    public AssetExportEventArgs(
        Asset asset,
        IAssetSource source,
        ExportConfiguration configuration,
        string relativeOutputDirectory)
        : base(asset, source)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        RelativeOutputDirectory = relativeOutputDirectory ?? throw new ArgumentNullException(nameof(relativeOutputDirectory));
    }
}
