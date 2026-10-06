using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;
using RedFox.IO.FileSystem;
using BaseAnimationHandler = RedFox.GameExtraction.AssetHandlers.AnimationHandler;

namespace ToolName.Handlers;

/// <summary>
/// Reads animation assets into scenes and exports them through the scene translators.
/// </summary>
public sealed class AnimationHandler : BaseAnimationHandler
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".seanim", ".xanim", ".anim", ".smd" };

    /// <summary>
    /// Determines whether this handler can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <param name="configuration">The shared settings and options.</param>
    /// <returns><see langword="true"/> when model support is enabled and the asset has a supported animation extension.</returns>
    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return configuration.GetOption("ModelSupport", true) && Extensions.Contains(Path.GetExtension(asset.Name));
    }

    /// <summary>
    /// Reads the animation entry through the scene translators.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="context">The read context for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A read result containing the scene produced by the translators.</returns>
    public override async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        SceneTranslatorManager translator = context.AssetManager.GetRequiredService<SceneTranslatorService>().Manager;

        await using Stream stream = OpenAssetStream(asset);
        await using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        Scene scene = await translator.ReadAsync(buffer, asset.Name, new(), cancellationToken).ConfigureAwait(false);

        return new AssetReadResult
        {
            Asset = asset,
            Data = scene,
            Handler = this,
        };
    }

    private static Stream OpenAssetStream(Asset asset) => asset.DataSource is VirtualFile file ? file.Open() : throw new InvalidOperationException($"AnimationHandler expects a {nameof(VirtualFile)} data source.");
}
