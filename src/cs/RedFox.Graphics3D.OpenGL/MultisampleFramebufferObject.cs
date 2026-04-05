using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class MultisampleFramebufferObject : IDisposable
{
    private readonly GL _gl;

    public uint FramebufferId { get; private set; }
    public uint ColorBufferId { get; private set; }
    public uint DepthBufferId { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int SampleCount { get; private set; }
    public bool IsInitialized => FramebufferId != 0 && ColorBufferId != 0;

    public MultisampleFramebufferObject(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

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

    public void BindForRendering()
    {
        if (FramebufferId != 0)
            _gl.BindFramebuffer(GLEnum.Framebuffer, FramebufferId);
    }

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
