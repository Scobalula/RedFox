using RedFox.GameExtraction;
using RedFox.IO.FileSystem;

namespace ToolName.Handlers;

/// <summary>
/// Reads and exports assets as raw byte-for-byte files.
/// </summary>
/// <remarks>
/// Registered last so specialized handlers take precedence.
/// </remarks>
public sealed class RawAssetHandler : IAssetHandler
{
    /// <summary>
    /// Determines whether this handler can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <param name="configuration">The shared settings and options.</param>
    /// <returns><see langword="true"/> when raw asset reading is enabled; otherwise, <see langword="false"/>.</returns>
    public bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return configuration.GetOption("ReadRawAssets", true);
    }

    /// <summary>
    /// Reads the full asset payload into memory from the asset data source.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="context">The read context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A typed read result containing the raw bytes.</returns>
    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        await using Stream stream = OpenAssetStream(asset);
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return new AssetReadResult
        {
            Asset = asset,
            Data = buffer.ToArray(),
            Handler = this,
        };
    }

    /// <summary>
    /// Determines whether the asset should be exported.
    /// </summary>
    /// <param name="asset">The asset being exported.</param>
    /// <param name="context">The export context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns><see langword="true"/> when export should continue; otherwise, <see langword="false"/>.</returns>
    public Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        string outputPath = context.ResolveAssetPath(asset);
        bool shouldExport = !File.Exists(outputPath) || context.Configuration.GetOption("Overwrite", false);
        return Task.FromResult(shouldExport);
    }

    /// <summary>
    /// Exports the raw bytes to disk using the asset's original extension when available.
    /// </summary>
    /// <param name="result">The read result containing raw bytes.</param>
    /// <param name="context">The export context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task that completes when the export has finished.</returns>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        string outputPath = context.ResolveAssetPath(result.Asset);

        if (File.Exists(outputPath) && !context.Configuration.GetOption("Overwrite", false))
        {
            return;
        }

        await AtomicFileWriter.WriteAllBytesAsync(outputPath, result.GetData<byte[]>(), cancellationToken).ConfigureAwait(false);
    }

    private static Stream OpenAssetStream(Asset asset) => asset.DataSource is VirtualFile file ? file.Open() : throw new InvalidOperationException($"RawAssetHandler expects a {nameof(VirtualFile)} data source.");
}
