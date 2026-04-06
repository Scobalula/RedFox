using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class ShadowFramebufferObject : IDisposable
{
    private readonly GL _gl;

    public uint FramebufferId { get; private set; }
    public uint DepthTextureId { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public bool IsInitialized => FramebufferId != 0 && DepthTextureId != 0;

    public ShadowFramebufferObject(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

    public void Initialize(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        Dispose();

        Width = width;
        Height = height;

        DepthTextureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, DepthTextureId);
        unsafe
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24,
                (uint)width, (uint)height, 0,
                PixelFormat.DepthComponent, PixelType.Float, null);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToBorder);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToBorder);
        _gl.TexParameter(TextureTarget.Texture2D, (TextureParameterName)GLEnum.TextureCompareMode, (int)GLEnum.None);
        float[] borderColor = [1.0f, 1.0f, 1.0f, 1.0f];
        unsafe
        {
            fixed (float* ptr = borderColor)
            {
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, ptr);
            }
        }

        FramebufferId = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferId);
        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            TextureTarget.Texture2D, DepthTextureId, 0);
        _gl.DrawBuffer(GLEnum.None);
        _gl.ReadBuffer(GLEnum.None);

        GLEnum status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"Shadow framebuffer initialization failed: {status}");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void BindForRendering()
    {
        if (FramebufferId != 0)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferId);
            _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        }
    }

    public void Dispose()
    {
        if (DepthTextureId != 0)
        {
            try { _gl.DeleteTexture(DepthTextureId); } catch { }
            DepthTextureId = 0;
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
