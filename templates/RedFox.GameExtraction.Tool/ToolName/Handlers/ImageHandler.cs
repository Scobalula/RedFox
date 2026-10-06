using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.Imaging;
using RedFox.Imaging.IO;
using RedFox.IO.FileSystem;
using BaseTextureHandler = RedFox.GameExtraction.AssetHandlers.TextureHandler;

namespace ToolName.Handlers;

/// <summary>
/// Reads image entries as lazily loaded textures and exports them through the image translators.
/// </summary>
public sealed class ImageHandler : BaseTextureHandler
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".dds", ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".tif", ".tiff", ".exr", ".ktx" };

    /// <summary>
    /// Determines whether this handler can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <param name="configuration">The shared settings and options.</param>
    /// <returns><see langword="true"/> when image support is enabled and the asset has a supported image extension.</returns>
    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return configuration.GetOption("ImageSupport", true) && Extensions.Contains(Path.GetExtension(asset.Name));
    }

    /// <summary>
    /// Reads the image from the asset data source backing the texture.
    /// </summary>
    /// <param name="texture">The texture created by the base handler.</param>
    /// <param name="translatorManager">The translators used to decode the asset.</param>
    /// <returns>The image stored in the asset.</returns>
    public override Image Load(Texture texture, ImageTranslatorManager translatorManager)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(translatorManager);

        Asset asset = texture.UserData as Asset ?? throw new InvalidOperationException($"Texture '{texture.Name}' is not backed by an asset.");

        using MemoryStream buffer = new();

        using (Stream stream = OpenAssetStream(asset))
        {
            stream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return translatorManager.Read(buffer, asset.Name);
    }

    private static Stream OpenAssetStream(Asset asset) => asset.DataSource is VirtualFile file ? file.Open() : throw new InvalidOperationException($"ImageHandler expects a {nameof(VirtualFile)} data source.");
}
