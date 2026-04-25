using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D;

/// <summary>
/// Represents a texture resource loaded from a file. Slot assignment lives on the
/// <see cref="MaterialTextureBinding"/> connection, not on the texture itself, so the
/// same <see cref="Texture"/> instance may be shared across multiple materials under
/// different slot keys.
/// </summary>
/// <param name="filePath">The full path to the image file to be used as the texture.</param>
public class Texture(string filePath) : SceneNode(Path.GetFileNameWithoutExtension(filePath))
{
    /// <summary>
    /// Gets or sets the full path to the file associated with this texture.
    /// </summary>
    public string FilePath { get; set; } = filePath;

    /// <summary>
    /// Gets or sets the resolved on-disk path for this texture.
    /// This can differ from <see cref="FilePath"/> when the source scene stores a relative reference.
    /// </summary>
    public string? ResolvedFilePath { get; set; }

    /// <summary>
    /// Gets the best path to use when loading the texture from disk.
    /// </summary>
    public string EffectiveFilePath => string.IsNullOrWhiteSpace(ResolvedFilePath) ? FilePath : ResolvedFilePath;

    /// <summary>
    /// Gets or sets the image data associated with this texture. This may be <see langword="null"/> if the image data has not been loaded yet.
    /// </summary>
    public Image? Data { get; set; }

    /// <summary>
    /// Gets the image format for the loaded texture payload.
    /// </summary>
    public ImageFormat Format => Data?.Format ?? ImageFormat.Unknown;

    /// <summary>
    /// Gets the raw pixel payload for the loaded texture data.
    /// </summary>
    public ReadOnlyMemory<byte> RawBytes => Data?.PixelMemory ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Gets or sets the image loader used to retrieve and process images.
    /// </summary>
    public IImageLoader? ImageLoader { get; set; } = FileSystemImageLoader.Shared;

    /// <inheritdoc/>
    public override IRenderHandle? CreateRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
    {
        return new TextureRenderHandle(graphicsDevice, this);
    }
}
