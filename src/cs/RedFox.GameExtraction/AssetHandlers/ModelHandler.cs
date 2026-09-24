using RedFox.Graphics3D;

namespace RedFox.GameExtraction.AssetHandlers;

/// <summary>
/// A standard <see cref="IAssetHandler"/> implementation for handling models. Implementations read a model into
/// an array of <see cref="Scene"/> instances, one per exported model (for example per LOD or variant), each of
/// which is self-contained and written to its own file named after the scene.
/// </summary>
public abstract class ModelHandler : IAssetHandler
{
    /// <summary>
    /// Gets the default formats to use during export.
    /// </summary>
    public static readonly string[] DefaultFormats = [".semodel", ".cast", ".fbx"];

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var imageManager = context.GetRequiredService<ImageTranslatorService>().Manager;
        var manager = context.AssetManager.GetRequiredService<SceneTranslatorService>().Manager;
        var scenes = result.GetData<Scene[]>();

        var outputDirectory = Path.Combine(context.OutputDirectory, result.Asset.Name);

        // TODO: Need to nail down a good API in asset manager so we can pass this onto the asset handler
        // but exporting images and where to export them to from the POV of a model is just so specific
        // it gets yucky and so I feel its best to let it handler it and pass it to a shared static method..
        // But I definitely need to look into how we can advise the sub-handler of a parent in a clean manner
        // including where to export a given image, etc. including potential for Model -> Material -> Texture
        var imageFormats = context.ExportConfiguration.GetOption("ImageFormats", TextureHandler.DefaultFormats);
        var relativeImages = context.ExportConfiguration.GetOption("RelativeModelImages", false);
        var relativeToMaterial = context.ExportConfiguration.GetOption("RelativeToMaterialImages", false);
        var skipExistingImages = context.ExportConfiguration.GetOption("SkipExistingImages", true);

        // Scenes hold their own copies of shared materials, so the same material is seen once per scene that uses it.
        var materials = scenes.SelectMany(scene => scene.EnumerateDescendants<Material>()).DistinctBy(material => material.Name);

        foreach (var material in materials)
        {
            foreach (var textureSlot in material.Textures)
            {
                var texture = textureSlot.Texture;

                string textureDirectory;
                string textureFileName = Path.GetFileName(texture.Name).Split('.')[0];

                if (relativeImages)
                    textureDirectory = Path.Combine(outputDirectory, "_images");
                else
                    textureDirectory = Path.Combine(context.OutputDirectory, Path.GetDirectoryName(texture.Name) ?? string.Empty);

                // We only export relative to material IF we are doing relative images at all
                if (relativeImages && relativeToMaterial)
                    textureDirectory = Path.Combine(textureDirectory, material.Name);

                TextureHandler.ExportTexture(texture, imageFormats, imageManager, textureDirectory, textureFileName, skipExistingImages);
            }
        }

        var modelFormats = context.ExportConfiguration.GetOption("ModelFormats", DefaultFormats);

        Directory.CreateDirectory(outputDirectory);

        foreach (var scene in scenes)
        {
            foreach (var modelFormat in modelFormats)
            {
                // WriteRawVertices will hint to the downstream translator that it should not apply skinning
                // We are reading and writing raw vertices from game formats so it's not needed.
                await manager.WriteAsync(Path.Combine(outputDirectory, scene.Name + modelFormat), scene, new() { WriteRawVertices = true }, cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (!context.ExportConfiguration.GetOption("SkipExistingModels", false))
            return true;

        // For now - a basic directory check is best we can do, as we would have no context on lods, formats, images, etc.
        // We could potentially do a wildcard check against let's say asset.Name*.semodel, etc.
        return !Directory.Exists(Path.Combine(context.OutputDirectory, asset.Name));
    }

    /// <inheritdoc/>
    public abstract bool CanHandle(Asset asset);

    /// <inheritdoc/>
    public abstract Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken);
}
