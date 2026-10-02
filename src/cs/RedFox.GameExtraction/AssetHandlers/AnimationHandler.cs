using RedFox.Graphics3D;

namespace RedFox.GameExtraction.AssetHandlers;

/// <summary>
/// A standard <see cref="IAssetHandler"/> implementation for handling animations. Implementations read an animation into a
/// self-contained <see cref="Scene"/> holding its animation, which is written to one file per configured format.
/// </summary>
public abstract class AnimationHandler : IAssetHandler
{
    /// <summary>
    /// Gets the default formats to use during export.
    /// </summary>
    public static readonly string[] DefaultFormats = [".seanim", ".cast"];

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manager = context.AssetManager.GetRequiredService<SceneTranslatorService>().Manager;
        var scene = result.GetData<Scene>();
        foreach (var format in context.Configuration.GetOption("AnimationFormats", DefaultFormats))
        {
            string outputPath = context.ResolveAssetPath(result.Asset, format);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await manager.WriteAsync(outputPath, scene, new(), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (!context.Configuration.GetOption("SkipExistingAnimations", false))
            return Task.FromResult(true);

        var formats = context.Configuration.GetOption("AnimationFormats", DefaultFormats);

        return Task.FromResult(!formats.All(format => File.Exists(context.ResolveAssetPath(asset, format))));
    }

    /// <inheritdoc/>
    public abstract bool CanHandle(Asset asset);

    /// <inheritdoc/>
    public abstract Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken);
}
