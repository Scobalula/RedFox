using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Handles uploading image data to OpenGL textures, supporting both
/// standard RGBA and block-compressed formats.
/// </summary>
internal sealed class GlTextureUploader
{
    private readonly GL _gl;
    private readonly ImageTranslatorManager? _translatorManager;

    public GlTextureUploader(GL gl, ImageTranslatorManager? translatorManager)
    {
        _gl = gl;
        _translatorManager = translatorManager;
    }

    /// <summary>
    /// Uploads a texture's image data to the GPU and returns a handle, or <c>null</c> if no image is available.
    /// </summary>
    public GLTextureHandle? Upload(Texture texture)
    {
        Image? image = texture.Data;

        if (image is null && texture.ImageLoader is not null && texture.ResolvedFilePath is not null && _translatorManager is not null)
            image = texture.ImageLoader.Load(texture.ResolvedFilePath, _translatorManager);

        string texturePath = texture.EffectiveFilePath;
        if (image is null && !string.IsNullOrWhiteSpace(texturePath) && File.Exists(texturePath))
            image = _translatorManager?.Read(texturePath);

        if (image is null)
            return null;

        if (ImageFormatInfo.IsBlockCompressed(image.Format) && GLTextureHandle.IsCompressedFormatSupported(image.Format))
            return new GLTextureHandle(_gl, image);

        return new GLTextureHandle(_gl, image.DecodeSlice<byte>(), image.Width, image.Height);
    }
}
