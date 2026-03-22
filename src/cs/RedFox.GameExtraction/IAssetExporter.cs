namespace RedFox.GameExtraction;

/// <summary>
/// Handles exporting assets from a game source.
/// </summary>
public interface IAssetExporter
{
    Task ExportAsync(
        IEnumerable<IAssetEntry> assets,
        string outputDirectory,
        SettingsBase settings,
        IProgress<ProgressInfo> progress,
        CancellationToken cancellationToken);
}