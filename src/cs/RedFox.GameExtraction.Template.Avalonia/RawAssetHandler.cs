using System.IO.Compression;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.Template.Avalonia;

/// <summary>
/// Reads and exports assets as raw byte-for-byte files.
/// </summary>
public sealed class RawAssetHandler : IAssetHandler
{
    /// <summary>
    /// Determines whether this handler can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <returns>Always <see langword="true"/> for this template handler.</returns>
    public bool CanHandle(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return true;
    }

    /// <summary>
    /// Reads the full asset payload into memory from the ZIP entry stored in <see cref="Asset.DataSource"/>.
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

        ZipArchiveEntry entry = asset.DataSource as ZipArchiveEntry
            ?? throw new InvalidOperationException($"RawAssetHandler expects a {nameof(ZipArchiveEntry)} data source.");

        await using Stream stream = entry.Open();
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return new AssetReadResult<byte[]>
        {
            Asset = asset,
            Data = buffer.ToArray(),
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
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(context);

        string outputPath = context.ResolveAssetPath(asset);
        bool shouldExport = !File.Exists(outputPath) || context.ExportConfiguration.Overwrite;
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

        AssetReadResult<byte[]> dataResult = result as AssetReadResult<byte[]>
            ?? throw new InvalidOperationException("RawAssetHandler expects byte[] read results.");

        string outputPath = context.ResolveAssetPath(result.Asset);

        if (File.Exists(outputPath) && !context.ExportConfiguration.Overwrite)
        {
            return;
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllBytesAsync(outputPath, dataResult.Data, cancellationToken).ConfigureAwait(false);
    }
}
