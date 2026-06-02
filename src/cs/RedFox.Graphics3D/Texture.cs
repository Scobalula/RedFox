using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D.Rendering;
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
public class Texture(string filePath) : SceneNode(filePath)
{
    internal bool LoadAttempted;

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
    /// Resolves <see cref="ResolvedFilePath"/> to an absolute on-disk path derived from
    /// <see cref="FilePath"/> and the supplied <paramref name="baseDirectory"/>. Data URIs and
    /// empty references leave <see cref="ResolvedFilePath"/> unset.
    /// </summary>
    /// <param name="baseDirectory">The directory relative references are resolved against, typically the source file's directory.</param>
    public void ResolveFilePath(string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(FilePath) || FilePath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            ResolvedFilePath = null;
            return;
        }

        if (System.IO.Path.IsPathRooted(FilePath))
        {
            ResolvedFilePath = System.IO.Path.GetFullPath(FilePath);
            return;
        }

        ResolvedFilePath = string.IsNullOrWhiteSpace(baseDirectory)
            ? null
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, FilePath));
    }

    /// <summary>
    /// Returns a portable path suitable for writing into a scene file: relative to
    /// <paramref name="targetDirectory"/> when an absolute path is available; otherwise the
    /// stored <see cref="FilePath"/> reference.
    /// </summary>
    /// <param name="targetDirectory">The directory the output file is being written to.</param>
    public string GetPortableFilePath(string? targetDirectory)
    {
        string effective = EffectiveFilePath;
        if (!string.IsNullOrWhiteSpace(effective)
            && System.IO.Path.IsPathRooted(effective)
            && !string.IsNullOrWhiteSpace(targetDirectory))
        {
            return System.IO.Path.GetRelativePath(targetDirectory, effective);
        }

        return FilePath;
    }

    /// <summary>
    /// Gets or sets the image data associated with this texture. This may be <see langword="null"/> if the image data has not been loaded yet.
    /// </summary>
    public Image? Data { get; set; }

    /// <summary>
    /// Gets the image format for the loaded texture payload.
    /// </summary>
    public ImageFormat Format => Data?.Format ?? ImageFormat.Unknown;

    /// <summary>
    /// Gets a value indicating whether the loaded texture payload is a cube map.
    /// </summary>
    public bool IsCubemap => Data?.IsCubemap == true;

    /// <summary>
    /// Gets the raw pixel payload for the loaded texture data.
    /// </summary>
    public ReadOnlyMemory<byte> RawBytes => Data?.PixelMemory ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Gets or sets the image loader used to retrieve and process images.
    /// </summary>
    public ITextureLoader? ImageLoader { get; set; } = FileSystemImageLoader.Shared;

    /// <summary>
    /// Loads image data through the supplied translator manager when this texture has no image data yet.
    /// </summary>
    /// <param name="translatorManager">The image translator manager used by the texture's loader.</param>
    /// <returns><see langword="true"/> when image data is available after the call; otherwise <see langword="false"/>.</returns>
    public bool TryLoad(ImageTranslatorManager translatorManager)
    {
        ArgumentNullException.ThrowIfNull(translatorManager);

        if (Data is not null)
        {
            return true;
        }

        if (ImageLoader is null || string.IsNullOrWhiteSpace(EffectiveFilePath))
        {
            return false;
        }

        try
        {
            Data = ImageLoader.Load(this, translatorManager);
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return Data is not null;
    }

    /// <inheritdoc/>
    public override IRenderHandle? CreateRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
    {
        return EnsureGraphicsHandle(graphicsDevice);
    }

    internal TextureRenderHandle EnsureGraphicsHandle(IGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        if (GraphicsHandle is TextureRenderHandle existingHandle && existingHandle.IsOwnedBy(graphicsDevice))
        {
            return existingHandle;
        }

        if (GraphicsHandle is not null)
        {
            GraphicsHandle.Release();
            GraphicsHandle.Dispose();
        }

        TextureRenderHandle textureHandle = new(graphicsDevice, this);
        GraphicsHandle = textureHandle;
        return textureHandle;
    }
}
