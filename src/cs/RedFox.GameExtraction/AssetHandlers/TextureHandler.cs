using RedFox.Imaging;
using RedFox.Imaging.Primitives;
using RedFox.Imaging.IO;
using RedFox.Graphics3D;

namespace RedFox.GameExtraction.AssetHandlers;

/// <summary>
/// A standard <see cref="IAssetHandler"/> implementation for handling textures.
/// </summary>
public abstract class TextureHandler : IAssetHandler, ITextureLoader
{
    /// <summary>
    /// Gets the default formats to use during export.
    /// </summary>
    public static readonly string[] DefaultFormats = [".png"];

    /// <inheritdoc/>
    public virtual async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        var manager = context.AssetManager.GetRequiredService<ImageTranslatorService>().Manager;
        var texture = result.GetData<Texture>();
        var skipExisting = context.ExportConfiguration.GetOption("SkipExistingImages", true);
        var imageFormats = context.ExportConfiguration.GetOption("ImageFormats", DefaultFormats);

        ExportTexture(texture, imageFormats, manager, context.ResolveOutputPath(texture.Name), skipExisting);
    }

    /// <inheritdoc/>
    public virtual async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // For textures, we simply pass on the object, data will be lazily loaded if needed
        return new AssetReadResult
        {
            Asset = asset,
            Data = new Texture(asset.Name)
            {
                UserData = asset,
                ImageLoader = this
            },
            Handler = this,
        };
    }

    /// <inheritdoc/>
    public virtual async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (!context.ExportConfiguration.GetOption("SkipExistingImages", true))
            return true;

        var imageFormats = context.ExportConfiguration.GetOption("ImageFormats", DefaultFormats);
        string fullPath = context.ResolveAssetPath(asset);

        foreach (var format in imageFormats)
        {
            if (!Path.Exists(Path.GetFullPath(Path.ChangeExtension(fullPath, format))))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public abstract bool CanHandle(Asset asset);

    /// <inheritdoc/>
    public abstract Image Load(Texture texture, ImageTranslatorManager translatorManager);

    /// <summary>
    /// Exports the given texture to each of the specified formats. The image is only decoded when at least one
    /// format has to be written, so exporting textures that already exist on disk is cheap.
    /// </summary>
    /// <param name="texture">The texture to export.</param>
    /// <param name="formats">The formats to export the texture to, as file extensions.</param>
    /// <param name="manager">The image translator manager.</param>
    /// <param name="path">The output path of the image, its extension is replaced by each format.</param>
    /// <param name="skipExisting">Whether to skip formats whose file already exists.</param>
    /// <returns>
    /// The full path of the image written for the last format, which models should reference, or the texture's current
    /// <see cref="Texture.FilePath"/> when no formats are provided.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the texture has no image loader, or its image could not be loaded.</exception>
    public static string ExportTexture(Texture texture, IReadOnlyList<string> formats, ImageTranslatorManager manager, string path, bool skipExisting)
    {
        Image? image = null;

        foreach (var format in formats)
        {
            var filePath = Path.GetFullPath(Path.ChangeExtension(path, format));

            if (skipExisting && File.Exists(filePath))
                continue;

            var loader = texture.ImageLoader ?? throw new InvalidOperationException($"Texture '{texture.Name}' does not have an image loader.");

            image ??= loader.Load(texture, manager) ?? throw new InvalidOperationException($"Texture '{texture.Name}' could not be loaded.");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            manager.Write(filePath, image);
        }

        return formats.Count > 0 ? Path.GetFullPath(Path.ChangeExtension(path, formats[^1])) : texture.FilePath;
    }
}
