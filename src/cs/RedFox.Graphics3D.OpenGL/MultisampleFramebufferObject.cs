using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Represents a multisampled OpenGL framebuffer with colour and depth renderbuffers.
/// Used as an intermediate MSAA render target that is resolved to the default framebuffer.
/// </summary>
public sealed class MultisampleFramebufferObject : IDisposable
{
    private readonly GL _gl;

    /// <summary>The OpenGL framebuffer object ID.</summary>
    public uint FramebufferId { get; private set; }

    /// <summary>The multisampled colour renderbuffer ID.</summary>
    public uint ColorBufferId { get; private set; }

    /// <summary>The multisampled depth renderbuffer ID.</summary>
    public uint DepthBufferId { get; private set; }

    /// <summary>The framebuffer width in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>The framebuffer height in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>The number of MSAA samples per pixel.</summary>
    public int SampleCount { get; private set; }

    /// <summary>Whether the framebuffer has been successfully initialised.</summary>
    public bool IsInitialized => FramebufferId != 0 && ColorBufferId != 0;

    /// <summary>
    /// Creates a new multisampled framebuffer wrapper bound to the given OpenGL context.
    /// </summary>
    public MultisampleFramebufferObject(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

    /// <summary>
    /// Allocates the multisampled framebuffer with colour and optional depth renderbuffers.
    /// Disposes any previously allocated resources first.
    /// </summary>
    /// <param name="width">Framebuffer width in pixels.</param>
    /// <param name="height">Framebuffer height in pixels.</param>
    /// <param name="sampleCount">Number of MSAA samples per pixel (must be greater than 1).</param>
    /// <param name="hasDepthBuffer">Whether to attach a depth renderbuffer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when dimensions or sample count are invalid.</exception>
    public void Initialize(int width, int height, int sampleCount, bool hasDepthBuffer = true)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Framebuffer width must be positive.");

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Framebuffer height must be positive.");

        if (sampleCount <= 1)
            throw new ArgumentOutOfRangeException(nameof(sampleCount), "MSAA sample count must be greater than one.");

        Dispose();

        Width = width;
        Height = height;
        SampleCount = sampleCount;

        FramebufferId = _gl.GenFramebuffer();
        _gl.BindFramebuffer(GLEnum.Framebuffer, FramebufferId);

        ColorBufferId = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, ColorBufferId);
        _gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, (uint)sampleCount, GLEnum.Rgba16f, (uint)width, (uint)height);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Renderbuffer, ColorBufferId);

        if (hasDepthBuffer)
        {
            DepthBufferId = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(GLEnum.Renderbuffer, DepthBufferId);
            _gl.RenderbufferStorageMultisample(GLEnum.Renderbuffer, (uint)sampleCount, GLEnum.DepthComponent24, (uint)width, (uint)height);
            _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, DepthBufferId);
        }

        GLEnum status = _gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"Multisample framebuffer initialization failed with status: {status}");

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);
    }

    /// <summary>Binds this framebuffer as the current render target.</summary>
    public void BindForRendering()
    {
        if (FramebufferId != 0)
            _gl.BindFramebuffer(GLEnum.Framebuffer, FramebufferId);
    }

    /// <summary>Deletes all GPU resources held by this framebuffer.</summary>
    public void Dispose()
    {
        if (DepthBufferId != 0)
        {
            try { _gl.DeleteRenderbuffer(DepthBufferId); } catch { }
            DepthBufferId = 0;
        }

        if (ColorBufferId != 0)
        {
            try { _gl.DeleteRenderbuffer(ColorBufferId); } catch { }
            ColorBufferId = 0;
        }

        if (FramebufferId != 0)
        {
            try { _gl.DeleteFramebuffer(FramebufferId); } catch { }
            FramebufferId = 0;
        }

        Width = 0;
        Height = 0;
        SampleCount = 0;
    }
}
