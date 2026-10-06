using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;
using RedFox.IO.FileSystem;
using BaseModelHandler = RedFox.GameExtraction.AssetHandlers.ModelHandler;

namespace ToolName.Handlers;

/// <summary>
/// Reads model assets into scenes and exports them through the scene translators.
/// </summary>
public sealed class ModelHandler : BaseModelHandler
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".semodel", ".xmodel", ".xmodel_export", ".mdl", ".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds" };

    /// <summary>
    /// Determines whether this handler can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <param name="configuration">The shared settings and options.</param>
    /// <returns><see langword="true"/> when model support is enabled and the asset has a supported model extension.</returns>
    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return configuration.GetOption("ModelSupport", true) && Extensions.Contains(Path.GetExtension(asset.Name));
    }

    /// <summary>
    /// Reads the model entry through the scene translators.
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
            Data = new[] { scene },
            Handler = this,
        };
    }

    private static Stream OpenAssetStream(Asset asset) => asset.DataSource is VirtualFile file ? file.Open() : throw new InvalidOperationException($"ModelHandler expects a {nameof(VirtualFile)} data source.");
}
