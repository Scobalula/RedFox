using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering;
using Silk.NET.OpenGL;
using System;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents a concrete OpenGL texture resource.
/// </summary>
internal sealed class OpenGlTexture : IGpuTexture
{
    private readonly GL _gl;

    /// <summary>
    /// Gets the GL texture or renderbuffer handle.
    /// </summary>
    internal uint Handle { get; private set; }

    /// <summary>
    /// Gets the texture width in pixels.
    /// </summary>
    internal int Width { get; }

    /// <summary>
    /// Gets the texture height in pixels.
    /// </summary>
    internal int Height { get; }

    /// <summary>
    /// Gets the texture format.
    /// </summary>
    internal ImageFormat Format { get; }

    /// <summary>
    /// Gets the texture sample count.
    /// </summary>
    internal int SampleCount { get; }

    /// <summary>
    /// Gets the texture target used when binding this resource.
    /// </summary>
    internal TextureTarget Target { get; }

    /// <summary>
    /// Gets a value indicating whether this resource is backed by a renderbuffer.
    /// </summary>
    internal bool IsRenderbuffer { get; }

    /// <summary>
    /// Gets the texture usage flags.
    /// </summary>
    internal TextureUsage Usage { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlTexture"/> class.
    /// </summary>
    /// <param name="gl">The owning GL instance.</param>
    /// <param name="handle">The GL texture handle.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The texture usage flags.</param>
    public OpenGlTexture(GL gl, uint handle, int width, int height, ImageFormat format, TextureUsage usage)
        : this(gl, handle, width, height, format, usage, 1, TextureTarget.Texture2D, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlTexture"/> class.
    /// </summary>
    /// <param name="gl">The owning GL instance.</param>
    /// <param name="handle">The GL texture handle.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The texture usage flags.</param>
    /// <param name="target">The texture target used when binding the resource.</param>
    public OpenGlTexture(GL gl, uint handle, int width, int height, ImageFormat format, TextureUsage usage, TextureTarget target)
        : this(gl, handle, width, height, format, usage, 1, target, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlTexture"/> class.
    /// </summary>
    /// <param name="gl">The owning GL instance.</param>
    /// <param name="handle">The GL texture handle.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The texture usage flags.</param>
    /// <param name="sampleCount">The texture sample count.</param>
    /// <param name="target">The texture target used when binding the resource.</param>
    public OpenGlTexture(GL gl, uint handle, int width, int height, ImageFormat format, TextureUsage usage, int sampleCount, TextureTarget target)
        : this(gl, handle, width, height, format, usage, sampleCount, target, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlTexture"/> class.
    /// </summary>
    /// <param name="gl">The owning GL instance.</param>
    /// <param name="handle">The GL resource handle.</param>
    /// <param name="width">The texture width.</param>
    /// <param name="height">The texture height.</param>
    /// <param name="format">The texture format.</param>
    /// <param name="usage">The texture usage flags.</param>
    /// <param name="sampleCount">The texture sample count.</param>
    /// <param name="target">The texture target used when binding the resource.</param>
    /// <param name="isRenderbuffer">Whether the resource is backed by a renderbuffer.</param>
    public OpenGlTexture(GL gl, uint handle, int width, int height, ImageFormat format, TextureUsage usage, int sampleCount, TextureTarget target, bool isRenderbuffer)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        Handle = handle;
        Width = width;
        Height = height;
        Format = format;
        Usage = usage;
        SampleCount = Math.Max(1, sampleCount);
        Target = target;
        IsRenderbuffer = isRenderbuffer;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        if (Handle != 0)
        {
            if (IsRenderbuffer)
            {
                _gl.DeleteRenderbuffer(Handle);
            }
            else
            {
                _gl.DeleteTexture(Handle);
            }

            Handle = 0;
        }

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}