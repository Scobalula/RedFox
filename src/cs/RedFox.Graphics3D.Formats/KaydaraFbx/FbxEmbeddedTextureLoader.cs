using RedFox.Imaging;
using RedFox.Imaging.IO;

namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Loads a texture from image data embedded in an FBX <c>Video</c> object.
/// </summary>
/// <param name="content">The embedded image file contents.</param>
public sealed class FbxEmbeddedTextureLoader(byte[] content) : ITextureLoader
{
    /// <summary>
    /// Gets the embedded image file contents.
    /// </summary>
    public byte[] Content { get; } = content;

    /// <inheritdoc/>
    public Image? Load(Texture texture, ImageTranslatorManager translatorManager, MipType mipType)
    {
        using MemoryStream stream = new(Content, writable: false);
        return translatorManager.Read(stream, texture.FilePath);
    }
}
