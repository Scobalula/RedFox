using System.IO.Compression;
using RedFox.Audio;
using RedFox.Audio.IO.Wav;
using RedFox.GameExtraction;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Reads WAVE entries as audio clips for preview and exports the original file bytes.
/// </summary>
public sealed class AudioHandler : IAssetHandler
{
    private readonly WavAudioTranslator _translator = new();

    /// <summary>
    /// Determines whether the asset is a WAVE file.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <param name="configuration">The shared settings and options.</param>
    /// <returns><see langword="true"/> when the asset has a .wav extension.</returns>
    public bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return string.Equals(Path.GetExtension(asset.Name), ".wav", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads and decodes the WAVE entry.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="context">The read context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A read result containing the <see cref="AudioClip"/>.</returns>
    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(context);

        byte[] data = await ReadAllBytesAsync(asset, cancellationToken).ConfigureAwait(false);

        return new AssetReadResult
        {
            Asset = asset,
            Data = _translator.Read(data),
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
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(context);

        string outputPath = context.ResolveAssetPath(asset);
        return Task.FromResult(!File.Exists(outputPath) || context.Configuration.GetOption("Overwrite", false));
    }

    /// <summary>
    /// Exports the original WAVE file bytes.
    /// </summary>
    /// <param name="result">The read result for the asset.</param>
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

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        byte[] data = await ReadAllBytesAsync(result.Asset, cancellationToken).ConfigureAwait(false);
        await AtomicFileWriter.WriteAllBytesAsync(outputPath, data, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Asset asset, CancellationToken cancellationToken)
    {
        await using Stream stream = asset.DataSource switch
        {
            VirtualFile file => file.Open(),
            ZipArchiveEntry entry => entry.Open(),
            _ => throw new InvalidOperationException($"AudioHandler expects a {nameof(VirtualFile)} or {nameof(ZipArchiveEntry)} data source."),
        };

        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
