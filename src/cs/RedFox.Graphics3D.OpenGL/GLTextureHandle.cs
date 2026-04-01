using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLTextureHandle : IDisposable
{
    public uint TextureId { get; private set; }
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

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgbaData);
        gl.GenerateMipmap(TextureTarget.Texture2D);
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Bind(uint unit)
    {
        _gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + (int)unit));
        _gl.BindTexture(TextureTarget.Texture2D, TextureId);
    }

    public void Dispose()
    {
        if (TextureId != 0)
        {
            try
            {
                _gl.DeleteTexture(TextureId);
            }
            catch
            {
            }
            TextureId = 0;
        }
    }
}
