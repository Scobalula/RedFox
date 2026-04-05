using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents an equirectangular environment map loaded from an HDR image file.
/// Uploads pixel data directly to the GPU as a floating-point RGBA texture.
/// </summary>
public sealed class GLEquirectangularEnvironmentMap : IDisposable
{
    private readonly GL _gl;

    /// <summary>
    /// Gets the fully-qualified path of the source HDR image file, or <c>null</c> if not yet loaded.
    /// </summary>
    public string? SourcePath { get; private set; }

    /// <summary>
    /// Gets the last-write time (UTC ticks) of the source file at the time metadata was loaded.
    /// </summary>
    public long SourceLastWriteTimeUtcTicks { get; private set; }

    /// <summary>
    /// Gets a lowercase hex-encoded SHA-256 hash of the source pixel data, or <c>null</c> if not yet computed.
    /// </summary>
    public string? SourcePixelHashHex { get; private set; }

    /// <summary>
    /// Gets whether the image should be flipped vertically when uploaded to the GPU.
    /// </summary>
    public bool EffectiveFlipY { get; private set; }

    /// <summary>
    /// Gets the OpenGL texture handle for this environment map.
    /// </summary>
    public GLHdrTextureHandle? TextureHandle { get; private set; }

    /// <summary>
    /// Gets whether the environment map has been successfully loaded.
    /// </summary>
    public bool IsLoaded => TextureHandle is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLEquirectangularEnvironmentMap"/> class.
    /// </summary>
    /// <param name="gl">The OpenGL context wrapper.</param>
    public GLEquirectangularEnvironmentMap(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

    /// <summary>
    /// Loads source metadata without decoding the underlying image file.
    /// This allows the env-map cache to be consulted before paying the EXR decode cost.
    /// </summary>
    public void LoadMetadata(string filePath, bool flipY = false)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        TextureHandle?.Dispose();
        TextureHandle = null;

        string fullPath = Path.GetFullPath(filePath);
        SourcePath = fullPath;
        EffectiveFlipY = flipY;
        SourcePixelHashHex = null;

        try
        {
            SourceLastWriteTimeUtcTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;
        }
        catch
        {
            SourceLastWriteTimeUtcTicks = 0;
        }

    }

    /// <summary>
    /// Loads an HDR image file (e.g., .exr) and uploads it to the GPU as a floating-point texture.
    /// The raw pixel data is passed directly to the GPU without intermediate decoding.
    /// </summary>
    /// <param name="filePath">The path to the HDR image file.</param>
    /// <param name="translatorManager">The image translator manager used to load the image.</param>
    /// <param name="flipY">If <c>true</c>, the image is flipped vertically on upload.</param>
    public void Load(string filePath, ImageTranslatorManager translatorManager, bool flipY = false)
    {
        LoadMetadata(filePath, flipY);
        EnsureTextureLoaded(translatorManager);
    }

    /// <summary>
    /// Ensures the GPU texture has been uploaded. If the texture is already loaded this returns immediately.
    /// </summary>
    /// <param name="translatorManager">The image translator manager used to decode the source file.</param>
    /// <returns><c>true</c> if the texture was successfully loaded or was already loaded; otherwise <c>false</c>.</returns>
    public bool EnsureTextureLoaded(ImageTranslatorManager translatorManager)
    {
        ArgumentNullException.ThrowIfNull(translatorManager);

        if (TextureHandle is not null)
            return true;

        if (string.IsNullOrWhiteSpace(SourcePath))
            return false;

        Image image = translatorManager.Read(SourcePath);

        try
        {
            byte[] hash = SHA256.HashData(image.PixelData);
            SourcePixelHashHex = Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            SourcePixelHashHex = null;
        }

        // Pass raw pixel data directly to GPU - no need to decode through Vector4
        // EXR images are already loaded as R32G32B32A32Float format
        TextureHandle = new GLHdrTextureHandle(_gl, image.PixelData, image.Width, image.Height);
        return true;
    }

    /// <summary>
    /// Releases the GPU texture and resets the handle to <c>null</c>.
    /// </summary>
    public void ReleaseTexture()
    {
        TextureHandle?.Dispose();
        TextureHandle = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ReleaseTexture();

        SourcePath = null;
        SourceLastWriteTimeUtcTicks = 0;
        SourcePixelHashHex = null;
        EffectiveFlipY = false;
    }
}
