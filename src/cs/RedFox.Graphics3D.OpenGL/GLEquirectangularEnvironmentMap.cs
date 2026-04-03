using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents an equirectangular environment map loaded from an HDR image file.
/// Uploads pixel data directly to the GPU as a floating-point RGBA texture.
/// </summary>
public sealed class GLEquirectangularEnvironmentMap : IDisposable
{
    private readonly GL _gl;

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
    /// Loads an HDR image file (e.g., .exr) and uploads it to the GPU as a floating-point texture.
    /// The raw pixel data is passed directly to the GPU without intermediate decoding.
    /// </summary>
    /// <param name="filePath">The path to the HDR image file.</param>
    /// <param name="translatorManager">The image translator manager used to load the image.</param>
    public void Load(string filePath, ImageTranslatorManager translatorManager)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(translatorManager);

        TextureHandle?.Dispose();
        TextureHandle = null;

        Image image = translatorManager.Read(filePath);

        // Pass raw pixel data directly to GPU - no need to decode through Vector4
        // EXR images are already loaded as R32G32B32A32Float format
        TextureHandle = new GLHdrTextureHandle(_gl, image.PixelData, image.Width, image.Height);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        TextureHandle?.Dispose();
        TextureHandle = null;
    }
}
