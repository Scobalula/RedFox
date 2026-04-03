using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLEquirectangularEnvironmentMap : IDisposable
{
    private readonly GL _gl;

    public GLHdrTextureHandle? TextureHandle { get; private set; }
    public bool IsLoaded => TextureHandle is not null;

    public GLEquirectangularEnvironmentMap(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
    }

    public void Load(string filePath, ImageTranslatorManager translatorManager)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(translatorManager);

        TextureHandle?.Dispose();
        TextureHandle = null;

        Image image = translatorManager.Read(filePath);
        Vector4[] pixels = image.DecodeSlice();

        float[] rgbaFloats = new float[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            rgbaFloats[(i * 4) + 0] = pixels[i].X;
            rgbaFloats[(i * 4) + 1] = pixels[i].Y;
            rgbaFloats[(i * 4) + 2] = pixels[i].Z;
            rgbaFloats[(i * 4) + 3] = pixels[i].W;
        }

        TextureHandle = new GLHdrTextureHandle(_gl, rgbaFloats, image.Width, image.Height);
    }

    public void Dispose()
    {
        TextureHandle?.Dispose();
        TextureHandle = null;
    }
}
