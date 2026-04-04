using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLEnvironmentResources : IDisposable
{
    private readonly GL _gl;

    public GLEnvironmentResources(
        GL gl,
        string cacheKey,
        int skySize,
        int skyMipLevels,
        int irradianceSize,
        int prefilterSize,
        int prefilterMipLevels)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        CacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));

        SkyCubemap = new GLCubemap(_gl);
        IrradianceCubemap = new GLCubemap(_gl);
        PrefilterCubemap = new GLCubemap(_gl);

        SkyCubemap.Create(skySize, skyMipLevels, useMipmaps: skyMipLevels > 1);
        IrradianceCubemap.Create(irradianceSize, 1, useMipmaps: false);
        PrefilterCubemap.Create(prefilterSize, prefilterMipLevels, useMipmaps: prefilterMipLevels > 1);
    }

    public string CacheKey { get; }

    public GLCubemap SkyCubemap { get; }
    public GLCubemap IrradianceCubemap { get; }
    public GLCubemap PrefilterCubemap { get; }

    public uint BrdfLutTexture { get; internal set; }

    public float SkyMaxMipLevel => Math.Max(SkyCubemap.MipLevels - 1, 0);
    public float PrefilterMaxMipLevel => Math.Max(PrefilterCubemap.MipLevels - 1, 0);

    public void Dispose()
    {
        SkyCubemap.Dispose();
        IrradianceCubemap.Dispose();
        PrefilterCubemap.Dispose();

        if (BrdfLutTexture != 0)
        {
            try { _gl.DeleteTexture(BrdfLutTexture); } catch { }
            BrdfLutTexture = 0;
        }
    }
}

