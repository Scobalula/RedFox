using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLTextureHandle
{
    public uint TextureId { get; }
    public int Width { get; }
    public int Height { get; }

    private readonly GL _gl;

    public GLTextureHandle(GL gl, byte[] rgbaData, int width, int height)
    {
        _gl = gl;
        Width = width;
        Height = height;

        TextureId = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, TextureId);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)LinearFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)LinearFilter.Linear);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);

        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgbaData);
        gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    public void Bind(uint unit)
    {
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, TextureId);
    }

    public void Dispose()
    {
        if (TextureId != 0)
        {
            _gl.DeleteTexture(TextureId);
            TextureId = 0;
        }
    }
}
