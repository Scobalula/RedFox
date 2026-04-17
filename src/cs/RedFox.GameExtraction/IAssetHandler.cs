namespace RedFox.GameExtraction;

/// <summary>
/// Reads and exports a specific class of assets.
/// </summary>
public interface IAssetHandler
{
    /// <summary>
    /// Determines whether the handler can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset to inspect.</param>
    /// <returns><see langword="true"/> when the handler can process the asset; otherwise, <see langword="false"/>.</returns>
    bool CanHandle(Asset asset);

    /// <summary>
    /// Reads an asset for preview or export.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="context">The read context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The read result.</returns>
    Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the supplied asset should be exported for the current configuration.
    /// </summary>
    /// <param name="asset">The asset that may be exported.</param>
    /// <param name="context">The export context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns><see langword="true"/> when the asset should be exported; otherwise, <see langword="false"/>.</returns>
    Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Exports a previously read asset result.
    /// </summary>
    /// <param name="result">The read result to export.</param>
    /// <param name="context">The export context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken);
}
