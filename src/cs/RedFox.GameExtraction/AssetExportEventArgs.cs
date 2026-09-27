namespace RedFox.GameExtraction;

/// <summary>
/// Provides event data for asset export notifications.
/// </summary>
/// <param name="asset">The asset being exported.</param>
/// <param name="source">The source that owns the asset.</param>
/// <param name="configuration">The export configuration used for the operation.</param>
/// <param name="relativeOutputDirectory">The relative output directory applied to the export when one is set.</param>
public class AssetExportEventArgs(Asset asset, IAssetSource source, ExportConfiguration configuration, string relativeOutputDirectory) : AssetOperationEventArgs(asset, source)
{
    /// <summary>
    /// Gets the export configuration used for the operation.
    /// </summary>
    public ExportConfiguration Configuration { get; } = configuration ?? throw new ArgumentNullException(nameof(configuration));

    /// <summary>
    /// Gets the relative output directory applied to the export when one is set.
    /// </summary>
    public string RelativeOutputDirectory { get; } = relativeOutputDirectory ?? throw new ArgumentNullException(nameof(relativeOutputDirectory));
}
