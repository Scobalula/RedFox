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

        string relativeAssetPath = context.ExportConfiguration.PreserveDirectoryStructure ? AssetManager.NormalizeVirtualPath(result.Asset.Name) : Path.GetFileName(AssetManager.NormalizeVirtualPath(result.Asset.Name));
        string outputDirectory = context.ResolveOutputDirectory(relativeAssetPath);

        var imageFormats = context.ExportConfiguration.GetOption("ImageFormats", TextureHandler.DefaultFormats);
        var relativeImages = context.ExportConfiguration.GetOption("RelativeModelImages", false);
        var relativeToMaterial = context.ExportConfiguration.GetOption("RelativeToMaterialImages", false);
        var skipExistingImages = context.ExportConfiguration.GetOption("SkipExistingImages", true);

        // Each scene holds its own clones of shared materials and textures, so every clone must point at the
        // exported image, while each image is only written once.
        var exportedImages = new Dictionary<string, string>();

        foreach (var material in scenes.SelectMany(scene => scene.EnumerateDescendants<Material>()))
        {
            foreach (var texture in material.Textures.Select(binding => binding.Texture))
            {
                string textureRelativePath = relativeImages ? Path.Combine(relativeAssetPath, "_images") : Path.GetDirectoryName(AssetManager.NormalizeVirtualPath(texture.Name)) ?? string.Empty;
                if (relativeImages && relativeToMaterial)
                    textureRelativePath = Path.Combine(textureRelativePath, material.Name);

                string textureName = Path.GetFileNameWithoutExtension(AssetManager.NormalizeVirtualPath(texture.Name));
                string texturePath = context.ResolveOutputPath(Path.Combine(textureRelativePath, textureName));

                if (!exportedImages.TryGetValue(texturePath, out var filePath))
                {
                    filePath = TextureHandler.ExportTexture(texture, imageFormats, imageManager, texturePath, skipExistingImages);
                    exportedImages[texturePath] = filePath;
                }

                texture.FilePath = filePath;
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
                string outputPath = context.ResolveOutputPath(Path.Combine(relativeAssetPath, scene.Name + modelFormat));
                await manager.WriteAsync(outputPath, scene, new() { WriteRawVertices = true }, cancellationToken);
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
        string relativeAssetPath = context.ExportConfiguration.PreserveDirectoryStructure ? AssetManager.NormalizeVirtualPath(asset.Name) : Path.GetFileName(AssetManager.NormalizeVirtualPath(asset.Name));
        return !Directory.Exists(context.ResolveOutputDirectory(relativeAssetPath));
    }

    /// <inheritdoc/>
    public abstract bool CanHandle(Asset asset);

    /// <inheritdoc/>
    public abstract Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken);
}
