using Silk.NET.OpenGL;
using System;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents an OpenGL texture for HDR images using 32-bit float per channel (RGBA16F/RGBA32F) format.
/// Accepts raw byte pixel data and uploads it directly to the GPU.
/// </summary>
public sealed class GLHdrTextureHandle : IDisposable
{
    private readonly GL _gl;

    /// <summary>
    /// Gets the OpenGL texture ID.
    /// </summary>
    public uint TextureId { get; private set; }

    /// <summary>
    /// Gets the width of the texture in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the texture in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Creates a new HDR texture from raw byte pixel data.
    /// The data is interpreted as 32-bit float RGBA (16 bytes per pixel).
    /// </summary>
    /// <param name="gl">The OpenGL context wrapper.</param>
    /// <param name="pixelData">Raw pixel data as bytes (must be width * height * 16 bytes for RGBA32F).</param>
    /// <param name="width">The width of the texture in pixels.</param>
    /// <param name="height">The height of the texture in pixels.</param>
    public unsafe GLHdrTextureHandle(GL gl, ReadOnlySpan<byte> pixelData, int width, int height)
    {
        _gl = gl;
        Width = width;
        Height = height;

        TextureId = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        fixed (byte* ptr = pixelData)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.Float, ptr);
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>
    /// Binds this texture to the specified texture unit.
    /// </summary>
    /// <param name="unit">The texture unit index (0, 1, 2, etc.).</param>
    public void Bind(uint unit)
    {
        _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)unit));
        _gl.BindTexture(TextureTarget.Texture2D, TextureId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (TextureId != 0)
        {
            try { _gl.DeleteTexture(TextureId); }
            catch { }
            TextureId = 0;
        }
    }
}
