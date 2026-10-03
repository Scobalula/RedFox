using RedFox.Audio;
using RedFox.Audio.IO;

namespace RedFox.GameExtraction.AssetHandlers;

/// <summary>
/// A standard <see cref="IAssetHandler"/> implementation for handling audio. Implementations read an asset into an
/// <see cref="AudioClip"/>, which is exported through the <see cref="AudioTranslatorService"/> to each configured format.
/// </summary>
public abstract class AudioClipHandler : IAssetHandler
{
    /// <summary>
    /// Gets the default formats to use during export.
    /// </summary>
    public static readonly string[] DefaultFormats = [".wav"];

    /// <inheritdoc/>
    public virtual Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manager = context.AssetManager.GetRequiredService<AudioTranslatorService>().Manager;
        var skipExisting = context.Configuration.GetOption("SkipExistingAudio", true);
        var audioFormats = context.Configuration.GetOption("AudioFormats", DefaultFormats);

        ExportClip(result.GetData<AudioClip>(), audioFormats, manager, context.ResolveAssetPath(result.Asset), skipExisting);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (!context.Configuration.GetOption("SkipExistingAudio", true))
            return Task.FromResult(true);

        var audioFormats = context.Configuration.GetOption("AudioFormats", DefaultFormats);
        string fullPath = context.ResolveAssetPath(asset);

        return Task.FromResult(audioFormats.Any(format => !File.Exists(Path.GetFullPath(Path.ChangeExtension(fullPath, format)))));
    }

    /// <inheritdoc/>
    public abstract bool CanHandle(Asset asset, GameExtractionConfiguration configuration);

    /// <inheritdoc/>
    public abstract Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Exports the given clip to each of the specified formats. Encoded audio is passed through without decoding
    /// when a format can store it as-is.
    /// </summary>
    /// <param name="clip">The clip to export.</param>
    /// <param name="formats">The formats to export the clip to, as file extensions.</param>
    /// <param name="manager">The audio translator manager.</param>
    /// <param name="path">The output path of the clip, its extension is replaced by each format.</param>
    /// <param name="skipExisting">Whether to skip formats whose file already exists.</param>
    public static void ExportClip(AudioClip clip, IReadOnlyList<string> formats, AudioTranslatorManager manager, string path, bool skipExisting) => ExportClip(clip, formats, manager, path, skipExisting, new AudioTranslatorOptions());

    /// <summary>
    /// Exports the given clip to each of the specified formats using the supplied translator options.
    /// </summary>
    /// <param name="clip">The clip to export.</param>
    /// <param name="formats">The formats to export the clip to, as file extensions.</param>
    /// <param name="manager">The audio translator manager.</param>
    /// <param name="path">The output path of the clip, its extension is replaced by each format.</param>
    /// <param name="skipExisting">Whether to skip formats whose file already exists.</param>
    /// <param name="options">The options passed to each translator.</param>
    public static void ExportClip(AudioClip clip, IReadOnlyList<string> formats, AudioTranslatorManager manager, string path, bool skipExisting, AudioTranslatorOptions options)
    {
        foreach (var format in formats)
        {
            var filePath = Path.GetFullPath(Path.ChangeExtension(path, format));

            if (skipExisting && File.Exists(filePath))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            manager.Write(filePath, clip, options);
        }
    }
}
