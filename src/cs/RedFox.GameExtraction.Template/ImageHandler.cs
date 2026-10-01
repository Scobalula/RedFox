using System.IO.Compression;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Imaging;
using RedFox.Imaging.IO;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Reads image entries as lazily loaded textures and exports them through the image translators.
/// </summary>
public sealed class ImageHandler : TextureHandler
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".dds", ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".tif", ".tiff", ".exr", ".ktx" };

    /// <summary>
    /// Determines whether the asset is an image the built-in translators can read.
    /// </summary>
    /// <param name="asset">The asset being evaluated.</param>
    /// <returns><see langword="true"/> when the asset has a supported image extension.</returns>
    public override bool CanHandle(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return Extensions.Contains(Path.GetExtension(asset.Name));
    }

    /// <summary>
    /// Reads the image from the archive entry backing the texture.
    /// </summary>
    /// <param name="texture">The texture created by <see cref="TextureHandler.ReadAsync"/>.</param>
    /// <param name="translatorManager">The translators used to decode the entry.</param>
    /// <returns>The image stored in the entry.</returns>
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

    private static Stream OpenAssetStream(Asset asset)
    {
        return asset.DataSource switch
        {
            VirtualFile file => file.Open(),
            ZipArchiveEntry entry => entry.Open(),
            _ => throw new InvalidOperationException($"ImageHandler expects a {nameof(VirtualFile)} or {nameof(ZipArchiveEntry)} data source."),
        };
    }
}
