using RedFox.Graphics3D;
using RedFox.Graphics3D.Groups;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedFox.GameExtraction.AssetHandlers;

/// <summary>
/// A standard <see cref="IAssetHandler"/> implementation for handling models.
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
        var scene = result.GetData<Scene>();

        var outputDirectory = Path.Combine(context.OutputDirectory, result.Asset.Name);
        var modelName = Path.GetFileNameWithoutExtension(result.Asset.Name);

        // TODO: Need to nail down a good API in asset manager so we can pass this onto the asset handler
        // but exporting images and where to export them to from the POV of a model is just so specific
        // it gets yucky and so I feel its best to let it handler it and pass it to a shared static method..
        // But I definitely need to look into how we can advise the sub-handler of a parent in a clean manner
        // including where to export a given image, etc. including potential for Model -> Material -> Texture
        var imageFormats = context.ExportConfiguration.GetOption("ImageFormats", TextureHandler.DefaultFormats);
        var relativeImages = context.ExportConfiguration.GetOption("RelativeModelImages", false);
        var relativeToMaterial = context.ExportConfiguration.GetOption("RelativeToMaterialImages", false);
        var skipExistingImages = context.ExportConfiguration.GetOption("SkipExistingImages", true);

        foreach (var material in scene.EnumerateDescendants<Material>())
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

        var skipExisting = context.ExportConfiguration.GetOption("SkipExistingModels", true);
        var modelFormats = context.ExportConfiguration.GetOption("ModelFormats", DefaultFormats);

        foreach (var group in scene.EnumerateChildren<MeshGroup>())
        {
            Directory.CreateDirectory(outputDirectory);

            group.Flags |= SceneNodeFlags.Selected;

            foreach (var modelFormat in modelFormats)
            {
                var lodPath = Path.Combine(outputDirectory, modelName + group.Name + modelFormat);

                //if (skipExisting && Path.Exists(Path.GetFullPath(lodPath)))
                //    continue;

                // WriteRawVertices will hint to the downstream translator that it should not apply skinning
                // We are reading and writing raw vertices from game formats so it's not needed.
                await manager.WriteAsync(lodPath, scene, new() { Filter = SceneNodeFlags.Selected, WriteRawVertices = true }, cancellationToken);
            }

            group.Flags ^= SceneNodeFlags.Selected;
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
