using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
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
        var fullPath = Path.Combine(context.OutputDirectory, texture.Name);
        var skipExisting = context.ExportConfiguration.GetOption("SkipExistingImages", true);
        var imageFormats = context.ExportConfiguration.GetOption("ImageFormats", DefaultFormats);

        if (Path.GetDirectoryName(fullPath) is string directory)
            Directory.CreateDirectory(directory);
        if (texture.ImageLoader is null)
            throw new InvalidOperationException("Texture data does not have an image loader.");

        var data = texture.ImageLoader.Load(texture, manager);

        foreach (var format in imageFormats)
        {
            // We will essentially assign the last written image
            // as this textures new "path" for models, etc.
            texture.FilePath = Path.GetFullPath(Path.ChangeExtension(fullPath, format));

            if (skipExisting && File.Exists(texture.FilePath))
                continue;

            manager.Write(texture.FilePath, data);
        }
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
        var fullPath = Path.Combine(context.OutputDirectory, asset.Name);

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
    /// Exports the given texture to the specified formats using the provided manager and updates the texture's file path accordingly.
    /// </summary>
    /// <param name="texture">The texture to export.</param>
    /// <param name="formats">The formats to export the texture to.</param>
    /// <param name="manager">The image translator manager.</param>
    /// <param name="directory">The directory to export the texture to.</param>
    /// <param name="imageName">The name of the image file.</param>
    /// <param name="skipExisting">Whether to skip existing files.</param>
    /// <exception cref="NullReferenceException">Thrown when the texture's image loader is null.</exception>
    public static void ExportTexture(Texture texture, IEnumerable<string> formats, ImageTranslatorManager manager, string? directory, string imageName, bool skipExisting)
    {
        if (texture.ImageLoader is null)
            throw new NullReferenceException(nameof(texture.ImageLoader));

        var data = texture.ImageLoader.Load(texture, manager);
        var path = imageName;

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, imageName);
        }

        foreach (var format in formats)
        {
            // We will essentially assign the last written image
            // as this textures new "path" for models, etc.
            texture.FilePath = Path.GetFullPath(Path.ChangeExtension(path, format));

            if (skipExisting && File.Exists(texture.FilePath))
                continue;

            manager.Write(texture.FilePath, data);
        }
    }
}
