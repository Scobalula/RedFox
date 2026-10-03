using System.IO.Compression;
using RedFox.Audio.IO;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Reads audio entries as audio clips through the audio translators and exports them to the configured audio formats.
/// </summary>
public sealed class AudioHandler : AudioClipHandler
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".wav", ".flac" };

    /// <summary>
    /// Determines whether the asset is audio the built-in translators can read.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <param name="configuration">The shared settings and options.</param>
    /// <returns><see langword="true"/> when the asset has a supported audio extension.</returns>
    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return Extensions.Contains(Path.GetExtension(asset.Name));
    }

    /// <summary>
    /// Reads the audio entry through the audio translators without decoding it.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="context">The read context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A read result containing the <see cref="RedFox.Audio.AudioClip"/>.</returns>
    public override async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(context);

        AudioTranslatorManager manager = context.AssetManager.GetRequiredService<AudioTranslatorService>().Manager;
        using MemoryStream buffer = new();

        await using (Stream stream = OpenAssetStream(asset))
        {
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        buffer.Position = 0;

        return new AssetReadResult
        {
            Asset = asset,
            Data = manager.Read(buffer, asset.Name),
            Handler = this,
        };
    }

    private static Stream OpenAssetStream(Asset asset)
    {
        return asset.DataSource switch
        {
            VirtualFile file => file.Open(),
            ZipArchiveEntry entry => entry.Open(),
            _ => throw new InvalidOperationException($"AudioHandler expects a {nameof(VirtualFile)} or {nameof(ZipArchiveEntry)} data source."),
        };
    }
}
