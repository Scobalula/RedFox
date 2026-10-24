using RedFox.Graphics3D.Rendering;
using Silk.NET.OpenGL;
using System;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents a concrete OpenGL framebuffer render target.
/// </summary>
internal sealed class OpenGlRenderTarget : IGpuRenderTarget
{
    private readonly GL _gl;

    /// <summary>
    /// Gets the GL framebuffer handle.
    /// </summary>
    internal uint Handle { get; private set; }

    /// <summary>
    /// Gets the color attachment texture.
    /// </summary>
    internal OpenGlTexture? ColorTexture { get; }

    /// <summary>
    /// Gets the optional depth attachment texture.
    /// </summary>
    internal OpenGlTexture? DepthTexture { get; }

    /// <summary>
    /// Gets the render target width in pixels.
    /// </summary>
    internal int Width => (ColorTexture ?? DepthTexture)!.Width;

    /// <summary>
    /// Gets the render target height in pixels.
    /// </summary>
    internal int Height => (ColorTexture ?? DepthTexture)!.Height;

    /// <summary>
    /// Gets the render target sample count.
    /// </summary>
    internal int SampleCount => (ColorTexture ?? DepthTexture)!.SampleCount;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGlRenderTarget"/> class.
    /// </summary>
    /// <param name="gl">The owning GL instance.</param>
    /// <param name="handle">The GL framebuffer handle.</param>
    /// <param name="colorTexture">The color attachment.</param>
    /// <param name="depthTexture">The optional depth attachment.</param>
    public OpenGlRenderTarget(GL gl, uint handle, OpenGlTexture? colorTexture, OpenGlTexture? depthTexture)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        Handle = handle;
        ColorTexture = colorTexture;
        DepthTexture = depthTexture;
        if (ColorTexture is null && DepthTexture is null)
        {
            throw new ArgumentException("A render target needs a color or a depth attachment.", nameof(colorTexture));
        }

        if (ColorTexture is not null && DepthTexture is not null && DepthTexture.SampleCount != ColorTexture.SampleCount)
        {
            throw new ArgumentException("Depth attachment sample count must match color attachment sample count.", nameof(depthTexture));
        }
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
            _gl.DeleteFramebuffer(Handle);
            Handle = 0;
        }

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}