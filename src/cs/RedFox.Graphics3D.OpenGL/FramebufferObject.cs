using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class FramebufferObject : IDisposable
{
    private readonly GL _gl;

    public uint FramebufferId { get; private set; }
    public uint TextureId { get; private set; }
    public uint DepthBufferId { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsInitialized => FramebufferId != 0 && TextureId != 0;

    public FramebufferObject(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

    public unsafe void Initialize(int width, int height, bool hasDepthBuffer = true)
    {
        Dispose();

        Width = width;
        Height = height;

        // Create framebuffer
        FramebufferId = _gl.GenFramebuffer();
        _gl.BindFramebuffer(GLEnum.Framebuffer, FramebufferId);

        // Create color attachment texture
        TextureId = _gl.GenTexture();
        _gl.BindTexture(GLEnum.Texture2D, TextureId);
        _gl.TexImage2D(
            GLEnum.Texture2D,
            0,
            (int)GLEnum.Rgba16f,
            (uint)width,
            (uint)height,
            0,
            GLEnum.Rgba,
            GLEnum.Float,
            null);

        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);

        _gl.FramebufferTexture2D(
            GLEnum.Framebuffer,
            GLEnum.ColorAttachment0,
            GLEnum.Texture2D,
            TextureId,
            0);

        // Create depth buffer if requested
        if (hasDepthBuffer)
        {
            DepthBufferId = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(GLEnum.Renderbuffer, DepthBufferId);
            _gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, (uint)width, (uint)height);
            _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, DepthBufferId);
        }

        // Check framebuffer status
        GLEnum status = _gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"Framebuffer initialization failed with status: {status}");
        }

        // Unbind
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.BindTexture(GLEnum.Texture2D, 0);
        if (DepthBufferId != 0)
            _gl.BindRenderbuffer(GLEnum.Renderbuffer, 0);
    }

    public void BindForRendering()
    {
        if (FramebufferId != 0)
            _gl.BindFramebuffer(GLEnum.Framebuffer, FramebufferId);
    }

    public void BindTexture(uint textureUnit)
    {
        if (TextureId != 0)
        {
            _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)textureUnit));
            _gl.BindTexture(GLEnum.Texture2D, TextureId);
        }
    }

    public void Clear()
    {
        if (FramebufferId != 0)
        {
            BindForRendering();
            _gl.Clear((uint)GLEnum.ColorBufferBit | (uint)GLEnum.DepthBufferBit);
        }
    }

    public void Dispose()
    {
        if (DepthBufferId != 0)
        {
            try { _gl.DeleteRenderbuffer(DepthBufferId); } catch { }
            DepthBufferId = 0;
        }

        if (TextureId != 0)
        {
            try { _gl.DeleteTexture(TextureId); } catch { }
            TextureId = 0;
        }

        if (FramebufferId != 0)
        {
            try { _gl.DeleteFramebuffer(FramebufferId); } catch { }
            FramebufferId = 0;
        }

        Width = 0;
        Height = 0;
    }
}
