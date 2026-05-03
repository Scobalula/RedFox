using System.Diagnostics;
using System.IO.Compression;
using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Reads and exports assets as raw byte-for-byte files.
/// </summary>
public sealed class ModelHandler : IAssetHandler
{
    /// <summary>
    /// Determines whether this handler can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <returns>Always <see langword="true"/> for this template handler.</returns>
    public bool CanHandle(Asset asset)
    {
        if (asset.Name.EndsWith(".semodel"))
            return true;

        return false;
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

        Console.WriteLine("Executing");

        var translator = context.AssetManager.GetRequiredService<SceneTranslatorService>().Manager;


        await using Stream stream = OpenAssetStream(asset);
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        buffer.Flush();

        var scene = await translator.ReadAsync(buffer, asset.Name, new(), cancellationToken);


        return new AssetReadResult<Scene>
        {
            Asset = asset,
            Data = scene,
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
            ?? throw new InvalidOperationException("ModelHandler expects byte[] read results.");

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

    private static Stream OpenAssetStream(Asset asset)
    {
        return asset.DataSource switch
        {
            VirtualFile file => file.Open(),
            ZipArchiveEntry entry => entry.Open(),
            _ => throw new InvalidOperationException(
                $"ModelHandler expects a {nameof(VirtualFile)} or {nameof(ZipArchiveEntry)} data source."),
        };
    }
}