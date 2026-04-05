using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Holds the GPU resources for image-based lighting (IBL) derived from an environment map,
/// including sky, irradiance, and prefilter cubemaps plus a BRDF lookup texture.
/// </summary>
public sealed class GLEnvironmentResources : IDisposable
{
    private readonly GL _gl;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLEnvironmentResources"/> class,
    /// creating empty cubemap textures with the specified dimensions.
    /// </summary>
    /// <param name="gl">The OpenGL context to use.</param>
    /// <param name="cacheKey">A unique key used to cache and look up these resources.</param>
    /// <param name="skySize">The resolution of each sky cubemap face, in pixels.</param>
    /// <param name="skyMipLevels">The number of mipmap levels for the sky cubemap.</param>
    /// <param name="irradianceSize">The resolution of each irradiance cubemap face, in pixels.</param>
    /// <param name="prefilterSize">The resolution of each prefilter cubemap face, in pixels.</param>
    /// <param name="prefilterMipLevels">The number of mipmap levels for the prefilter cubemap.</param>
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

    /// <summary>The cache key uniquely identifying this set of environment resources.</summary>
    public string CacheKey { get; }

    /// <summary>The sky cubemap texture used for skybox rendering.</summary>
    public GLCubemap SkyCubemap { get; }

    /// <summary>The irradiance cubemap used for diffuse image-based lighting.</summary>
    public GLCubemap IrradianceCubemap { get; }

    /// <summary>The prefilter cubemap used for specular image-based lighting.</summary>
    public GLCubemap PrefilterCubemap { get; }

    /// <summary>The OpenGL texture ID of the BRDF lookup table, or 0 if not yet generated.</summary>
    public uint BrdfLutTexture { get; internal set; }

    /// <summary>The maximum mip level of the sky cubemap (zero-indexed).</summary>
    public float SkyMaxMipLevel => Math.Max(SkyCubemap.MipLevels - 1, 0);

    /// <summary>The maximum mip level of the prefilter cubemap (zero-indexed).</summary>
    public float PrefilterMaxMipLevel => Math.Max(PrefilterCubemap.MipLevels - 1, 0);

    /// <summary>Disposes all GPU resources held by this instance.</summary>
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
