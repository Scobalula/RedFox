using System.IO;
using System.Numerics;
using System.Text.Json;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

/// <summary>
/// Precomputes IBL data following the learnopengl.com PBR pipeline:
///   1. Equirectangular -> cubemap conversion
///   2. Irradiance cubemap (diffuse)
///   3. Prefiltered environment cubemap (specular, mipmapped)
///   4. BRDF LUT 2D texture
///
/// All precomputed cubemaps are cached to disk next to the source environment map
/// so subsequent loads skip regeneration.
/// </summary>
public sealed class IblPrecomputePass : IRenderPass
{
    private GL _gl = null!;

    private GLShader _equirectToCubemapShader = null!;
    private GLShader _cubemapDownsampleShader = null!;
    private GLShader _irradianceShader = null!;
    private GLShader _prefilterShader = null!;
    private GLShader _brdfLutShader = null!;

    private uint _cubeVAO;
    private uint _cubeVBO;
    private uint _emptyVao;

    private GLEnvironmentResources? _resources;

    private bool _initialized;
    private bool _computed;

    public const int IrradianceSize = 32;
    public const int PrefilterSize = 256;
    public const int BrdfLutSize = 256;
    private const int DiffuseShProjectionFaceSize = 64;
    public static int PrefilterMipLevels => ComputeFullMipChainLevelCount(PrefilterSize);

    public string Name => "IBL Precompute";
    public bool Enabled { get; set; } = true;
    public bool Computed => _computed;
    public uint SkyCubemap => _resources?.SkyCubemap.TextureId ?? 0;
    public uint IrradianceCubemap => _resources?.IrradianceCubemap.TextureId ?? 0;
    public uint PrefilterCubemap => _resources?.PrefilterCubemap.TextureId ?? 0;
    public uint BrdfLutTexture => _resources?.BrdfLutTexture ?? 0;

    /// <summary>
    /// Maximum mip level index of the prefilter cubemap (used in textureLod).
    /// </summary>
    public float PrefilterMaxMipLevel => _resources?.PrefilterMaxMipLevel ?? (PrefilterMipLevels - 1);

    public float SkyMaxMipLevel => _resources?.SkyMaxMipLevel ?? 0;

    private string? _cacheDirectory;
    private EnvironmentCacheManifest? _expectedManifest;

    public unsafe void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;

        // Load shaders
        (string cv, string _) = ShaderSource.LoadProgram(_gl, "cubemap");
        (string _, string ef) = ShaderSource.LoadProgram(_gl, "equirect_to_cubemap");
        _equirectToCubemapShader = new GLShader(_gl, cv, ef);

        (string dv, string df) = ShaderSource.LoadProgram(_gl, "cubemap_downsample");
        _cubemapDownsampleShader = new GLShader(_gl, dv, df);

        (string iv, string ir) = ShaderSource.LoadProgram(_gl, "irradiance");
        _irradianceShader = new GLShader(_gl, iv, ir);

        (string pv, string pf) = ShaderSource.LoadProgram(_gl, "prefilter");
        _prefilterShader = new GLShader(_gl, pv, pf);

        (string bv, string bf) = ShaderSource.LoadProgram(_gl, "brdf_lut");
        _brdfLutShader = new GLShader(_gl, bv, bf);

        // Create cube VAO/VBO
        _cubeVAO = _gl.GenVertexArray();
        _cubeVBO = _gl.GenBuffer();
        _gl.BindVertexArray(_cubeVAO);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _cubeVBO);
        fixed (float* ptr = CubeGeometry.Vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(CubeGeometry.Vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        }
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, (void*)0);
        _gl.BindVertexArray(0);

        _emptyVao = _gl.GenVertexArray();

        _initialized = true;
    }

    public unsafe void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled)
            return;

        GLEquirectangularEnvironmentMap? envMap = renderer.EnvironmentMap;
        if (envMap is null || string.IsNullOrWhiteSpace(envMap.SourcePath))
            return;

        string sourcePath = envMap.SourcePath ?? "unknown";
        bool flipY = envMap.EffectiveFlipY;
        int skySize = 1024;
        int skyMipLevels = ComputeFullMipChainLevelCount(skySize);

        string key = EnvironmentCacheKey.Compute(
            sourcePath,
            envMap.SourceLastWriteTimeUtcTicks,
            flipY,
            skySize,
            skyMipLevels,
            IrradianceSize,
            PrefilterSize,
            PrefilterMipLevels,
            BrdfLutSize);

        if (_resources is not null && _resources.CacheKey.Equals(key, StringComparison.Ordinal))
        {
            _computed = true;
            return;
        }

        _expectedManifest = new EnvironmentCacheManifest
        {
            CacheVersion = EnvironmentCacheKey.CacheVersion,
            Key = key,
            SourcePath = sourcePath,
            SourceLastWriteTimeUtcTicks = envMap.SourceLastWriteTimeUtcTicks,
            SourcePixelHashHex = envMap.SourcePixelHashHex,
            FlipY = flipY,
            SkySize = skySize,
            SkyMipLevels = skyMipLevels,
            IrradianceSize = IrradianceSize,
            PrefilterSize = PrefilterSize,
            PrefilterMipLevels = PrefilterMipLevels,
            BrdfLutSize = BrdfLutSize,
        };

        _cacheDirectory = GetCacheDirectory(key);

        // Try cache first
        if (TryLoadFromCache(renderer, _expectedManifest, _cacheDirectory))
        {
            _computed = true;
            return;
        }

        if (renderer.ImageTranslatorManager is null || !envMap.EnsureTextureLoaded(renderer.ImageTranslatorManager))
            return;

        _expectedManifest.SourcePixelHashHex = envMap.SourcePixelHashHex;

        // Save viewport
        int* vp = stackalloc int[4];
        _gl.GetInteger(GLEnum.Viewport, vp);
        int savedX = vp[0], savedY = vp[1], savedW = vp[2], savedH = vp[3];
        _gl.GetInteger(GLEnum.DrawFramebufferBinding, out int drawFbo);
        int savedFbo = drawFbo;

        // Cubemap capture renders a box around the origin and needs depth testing so
        // only the nearest cube face contributes for each fragment.
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);

        var resources = new GLEnvironmentResources(_gl, key, skySize, skyMipLevels, IrradianceSize, PrefilterSize, PrefilterMipLevels);

        // 1. Convert equirectangular -> sky cubemap (mipmapped)
        ConvertEquirectangularToCubemap(envMap, resources.SkyCubemap, skySize);

        // 2. Generate irradiance cubemap
        GenerateIrradianceMap(resources.SkyCubemap.TextureId, skySize, resources.IrradianceCubemap.TextureId);

        // 3. Generate prefiltered environment cubemap
        GeneratePrefilterMap(resources.SkyCubemap.TextureId, skySize, resources.PrefilterCubemap.TextureId);

        // 4. Generate BRDF LUT
        resources.BrdfLutTexture = GenerateBrdfLut();

        renderer.SetEnvironmentResources(resources);
        _resources = resources;

        SaveToCache(_expectedManifest, _cacheDirectory, resources);
        envMap.ReleaseTexture();

        // Restore state
        _gl.Viewport(savedX, savedY, (uint)savedW, (uint)savedH);
        _gl.BindFramebuffer(GLEnum.Framebuffer, (uint)savedFbo);
        _gl.Enable(EnableCap.DepthTest);
        if (renderer.EnableBackFaceCulling)
            _gl.Enable(EnableCap.CullFace);
        else
            _gl.Disable(EnableCap.CullFace);

        _computed = true;
    }

    // ------------------------------------------------------------------
    // Cache
    // ------------------------------------------------------------------

    private static int ComputeFullMipChainLevelCount(int baseSize)
    {
        int levels = 1;
        int size = Math.Max(baseSize, 1);
        while (size > 1)
        {
            size >>= 1;
            levels++;
        }
        return levels;
    }

    private string GetCacheDirectory(string key)
    {
        string cacheBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EnvironmentCache");
        Directory.CreateDirectory(cacheBase);
        return Path.Combine(cacheBase, key);
    }

    private bool TryLoadFromCache(GLRenderer renderer, EnvironmentCacheManifest expected, string cacheDirectory)
    {
        try
        {
            string manifestPath = Path.Combine(cacheDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
                return false;

            EnvironmentCacheManifest? loaded = JsonSerializer.Deserialize<EnvironmentCacheManifest>(File.ReadAllText(manifestPath));
            if (!ManifestMatches(loaded, expected))
                return false;

            string skyPath = Path.Combine(cacheDirectory, "sky.foxmap");
            string irrPath = Path.Combine(cacheDirectory, "irradiance.foxmap");
            string prePath = Path.Combine(cacheDirectory, "prefilter.foxmap");
            string brdfPath = Path.Combine(cacheDirectory, "brdf_lut.dat");

            var resources = new GLEnvironmentResources(
                _gl,
                expected.Key,
                expected.SkySize,
                expected.SkyMipLevels,
                expected.IrradianceSize,
                expected.PrefilterSize,
                expected.PrefilterMipLevels);

            bool skyOk = GLCubemap.CacheExists(skyPath, expected.SkySize, expected.SkyMipLevels) &&
                         resources.SkyCubemap.LoadFromFile(skyPath);
            bool irrOk = GLCubemap.CacheExists(irrPath, expected.IrradianceSize, 1) &&
                         resources.IrradianceCubemap.LoadFromFile(irrPath);
            bool preOk = GLCubemap.CacheExists(prePath, expected.PrefilterSize, expected.PrefilterMipLevels) &&
                         resources.PrefilterCubemap.LoadFromFile(prePath);

            if (!skyOk || !irrOk || !preOk)
            {
                resources.Dispose();
                return false;
            }

            if (File.Exists(brdfPath))
            {
                resources.BrdfLutTexture = LoadBrdfLut(brdfPath);
            }
            else
            {
                resources.BrdfLutTexture = GenerateBrdfLut();
                SaveBrdfLut(brdfPath, resources.BrdfLutTexture);
            }

            renderer.SetEnvironmentResources(resources);
            _resources = resources;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ManifestMatches(EnvironmentCacheManifest? loaded, EnvironmentCacheManifest expected)
    {
        if (loaded is null)
            return false;

        return loaded.CacheVersion == expected.CacheVersion &&
               string.Equals(loaded.Key, expected.Key, StringComparison.Ordinal) &&
               string.Equals(loaded.SourcePath, expected.SourcePath, StringComparison.OrdinalIgnoreCase) &&
               loaded.SourceLastWriteTimeUtcTicks == expected.SourceLastWriteTimeUtcTicks &&
               MatchesOptionalPixelHash(loaded.SourcePixelHashHex, expected.SourcePixelHashHex) &&
               loaded.FlipY == expected.FlipY &&
               loaded.SkySize == expected.SkySize &&
               loaded.SkyMipLevels == expected.SkyMipLevels &&
               loaded.IrradianceSize == expected.IrradianceSize &&
               loaded.PrefilterSize == expected.PrefilterSize &&
               loaded.PrefilterMipLevels == expected.PrefilterMipLevels &&
               loaded.BrdfLutSize == expected.BrdfLutSize;
    }

    private static bool MatchesOptionalPixelHash(string? loaded, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return true;

        return string.Equals(loaded ?? string.Empty, expected, StringComparison.OrdinalIgnoreCase);
    }

    private void SaveToCache(EnvironmentCacheManifest expected, string cacheDirectory, GLEnvironmentResources resources)
    {
        try
        {
            Directory.CreateDirectory(cacheDirectory);

            resources.SkyCubemap.SaveToFile(Path.Combine(cacheDirectory, "sky.foxmap"));
            resources.IrradianceCubemap.SaveToFile(Path.Combine(cacheDirectory, "irradiance.foxmap"));
            resources.PrefilterCubemap.SaveToFile(Path.Combine(cacheDirectory, "prefilter.foxmap"));
            SaveBrdfLut(Path.Combine(cacheDirectory, "brdf_lut.dat"), resources.BrdfLutTexture);

            string manifestPath = Path.Combine(cacheDirectory, "manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(expected));
        }
        catch
        {
            // Cache save is non-critical
        }
    }

    // ------------------------------------------------------------------
    // Equirectangular -> Cubemap conversion
    // ------------------------------------------------------------------

    private unsafe void ConvertEquirectangularToCubemap(GLEquirectangularEnvironmentMap envMap, GLCubemap destination, int size)
    {
        using GLCubemap sourceCubemap = new(_gl);
        sourceCubemap.Create(size, 1, useMipmaps: false);

        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24, (uint)size, (uint)size);

        // Projection for cubemap faces: 90deg FOV, 1:1 aspect
        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();

        _equirectToCubemapShader.Use();
        _equirectToCubemapShader.SetUniform("uProjection", captureProjection);
        envMap.TextureHandle!.Bind(0);
        _equirectToCubemapShader.SetUniform("uEquirectangularMap", 0);
        _equirectToCubemapShader.SetUniform("uFlipY", envMap.EffectiveFlipY);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
            GLEnum.Renderbuffer, captureRBO);
        _gl.Viewport(0, 0, (uint)size, (uint)size);
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);

        _gl.BindVertexArray(_cubeVAO);

        for (int face = 0; face < 6; face++)
        {
            _equirectToCubemapShader.SetUniform("uView", captureViews[face]);
            TextureTarget target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
                target, sourceCubemap.TextureId, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);

            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
                target, destination.TextureId, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
        }

        _gl.BindVertexArray(0);

        // Generate cascaded mipmap chain: each level reads from the previous one
        // using a 5×5 Gaussian-weighted kernel for smooth HDR downsampling.
        GenerateSkyMipChain(sourceCubemap.TextureId, destination, size);

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);
    }

    private unsafe void GenerateSkyMipChain(uint sourceCubemap, GLCubemap destination, int size)
    {
        if (destination.MipLevels <= 1)
            return;

        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();

        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();

        _cubemapDownsampleShader.Use();
        _cubemapDownsampleShader.SetUniform("uProjection", captureProjection);
        _cubemapDownsampleShader.SetUniform("uBaseResolution", (float)size);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.TextureCubeMap, sourceCubemap);
        _cubemapDownsampleShader.SetUniform("uEnvironmentMap", 0);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.BindVertexArray(_cubeVAO);
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);

        for (int level = 1; level < destination.MipLevels; level++)
        {
            int mipSize = Math.Max(size >> level, 1);
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24, (uint)mipSize, (uint)mipSize);
            _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, captureRBO);
            _gl.Viewport(0, 0, (uint)mipSize, (uint)mipSize);
            _cubemapDownsampleShader.SetUniform("uTargetMipLevel", (float)level);

            for (int face = 0; face < 6; face++)
            {
                _cubemapDownsampleShader.SetUniform("uView", captureViews[face]);
                TextureTarget target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
                _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, target, destination.TextureId, level);
                _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
            }
        }

        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);
    }

    // ------------------------------------------------------------------
    // Irradiance map generation
    // ------------------------------------------------------------------

    private unsafe void GenerateIrradianceMap(uint envCubemap, int envCubemapResolution, uint destinationCubemap)
    {
        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24,
            (uint)IrradianceSize, (uint)IrradianceSize);

        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();
        Vector3[] shCoefficients = ComputeDiffuseIrradianceShCoefficients(envCubemap, envCubemapResolution);

        _irradianceShader.Use();
        _irradianceShader.SetUniform("uProjection", captureProjection);
        for (int i = 0; i < shCoefficients.Length; i++)
            _irradianceShader.SetUniform($"uShCoefficients[{i}]", shCoefficients[i]);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
            GLEnum.Renderbuffer, captureRBO);
        _gl.Viewport(0, 0, (uint)IrradianceSize, (uint)IrradianceSize);
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);

        _gl.BindVertexArray(_cubeVAO);

        for (int face = 0; face < 6; face++)
        {
            _irradianceShader.SetUniform("uView", captureViews[face]);
            var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
                target, destinationCubemap, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);
    }

    // ------------------------------------------------------------------
    // Prefilter map generation
    // ------------------------------------------------------------------

    private unsafe void GeneratePrefilterMap(uint envCubemap, int envCubemapResolution, uint destinationCubemap)
    {
        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();

        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();

        _prefilterShader.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.TextureCubeMap, envCubemap);
        _prefilterShader.SetUniform("uEnvironmentMap", 0);
        _prefilterShader.SetUniform("uProjection", captureProjection);
        _prefilterShader.SetUniform("uEnvMapResolution", (float)envCubemapResolution);
        _prefilterShader.SetUniform("uSourceMaxMipLevel", (float)(ComputeFullMipChainLevelCount(envCubemapResolution) - 1));

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.BindVertexArray(_cubeVAO);
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);

        for (int level = 0; level < PrefilterMipLevels; level++)
        {
            int mipWidth = Math.Max(PrefilterSize >> level, 1);
            int mipHeight = Math.Max(PrefilterSize >> level, 1);
            float roughness = (float)level / (PrefilterMipLevels - 1);

            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24,
                (uint)mipWidth, (uint)mipHeight);
            _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
                GLEnum.Renderbuffer, captureRBO);

            _gl.Viewport(0, 0, (uint)mipWidth, (uint)mipHeight);

            _prefilterShader.SetUniform("uRoughness", roughness);

            for (int face = 0; face < 6; face++)
            {
                _prefilterShader.SetUniform("uView", captureViews[face]);
                var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
                _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
                    target, destinationCubemap, level);
                _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
            }
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);
    }

    // ------------------------------------------------------------------
    // BRDF LUT
    // ------------------------------------------------------------------

    private unsafe uint GenerateBrdfLut()
    {
        uint fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(GLEnum.Framebuffer, fbo);

        // Create BRDF LUT texture
        uint brdfTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, brdfTexture);
        float[] emptyData = new float[BrdfLutSize * BrdfLutSize * 4];
        fixed (float* ptr = emptyData)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, (uint)BrdfLutSize, (uint)BrdfLutSize, 0,
                PixelFormat.Rgba, PixelType.Float, ptr);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
            GLEnum.Texture2D, brdfTexture, 0);
        _gl.Viewport(0, 0, (uint)BrdfLutSize, (uint)BrdfLutSize);

        _brdfLutShader.Use();
        _gl.Disable(EnableCap.DepthTest);
        _gl.BindVertexArray(_emptyVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);
        _gl.Enable(EnableCap.DepthTest);

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteFramebuffer(fbo);
        return brdfTexture;
    }

    private void SaveBrdfLut(string path, uint textureId)
    {
        float[] data = new float[BrdfLutSize * BrdfLutSize * 4];
        unsafe
        {
            fixed (float* ptr = data)
            {
                _gl.BindTexture(TextureTarget.Texture2D, textureId);
                _gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.Float, ptr);
            }
        }
        File.WriteAllBytes(path, System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan()).ToArray());
    }

    private uint LoadBrdfLut(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int expectedFloats = BrdfLutSize * BrdfLutSize * 4;
        if (data.Length != expectedFloats * sizeof(float))
        {
            return GenerateBrdfLut();
        }

        uint brdfTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, brdfTexture);
        unsafe
        {
            fixed (byte* ptr = data)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, BrdfLutSize, BrdfLutSize, 0,
                    PixelFormat.Rgba, PixelType.Float, ptr);
            }
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return brdfTexture;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Matrix4x4[] GetCaptureViews() =>
    [
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(-1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f,  1.0f,  0.0f), new Vector3(0.0f,  0.0f,  1.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f, -1.0f,  0.0f), new Vector3(0.0f,  0.0f, -1.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f,  0.0f,  1.0f), new Vector3(0.0f, -1.0f,  0.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f,  0.0f, -1.0f), new Vector3(0.0f, -1.0f,  0.0f)),
    ];

    private unsafe Vector3[] ComputeDiffuseIrradianceShCoefficients(uint envCubemap, int envCubemapResolution)
    {
        int maxMipLevel = ComputeFullMipChainLevelCount(envCubemapResolution) - 1;
        int projectionMipLevel = ChooseDiffuseShProjectionMipLevel(envCubemapResolution, maxMipLevel);
        int sampleSize = Math.Max(envCubemapResolution >> projectionMipLevel, 1);

        float[] faceData = new float[sampleSize * sampleSize * 4];
        Vector3[] coefficients = new Vector3[9];
        Span<float> basis = stackalloc float[9];

        _gl.BindTexture(TextureTarget.TextureCubeMap, envCubemap);

        for (int face = 0; face < 6; face++)
        {
            var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            fixed (float* ptr = faceData)
            {
                _gl.GetTexImage(target, projectionMipLevel, PixelFormat.Rgba, PixelType.Float, ptr);
            }

            for (int y = 0; y < sampleSize; y++)
            {
                float y0 = 1.0f - (2.0f * y / sampleSize);
                float y1 = 1.0f - (2.0f * (y + 1) / sampleSize);
                float faceV = 1.0f - (2.0f * (y + 0.5f) / sampleSize);

                for (int x = 0; x < sampleSize; x++)
                {
                    float x0 = (2.0f * x / sampleSize) - 1.0f;
                    float x1 = (2.0f * (x + 1) / sampleSize) - 1.0f;
                    float faceU = (2.0f * (x + 0.5f) / sampleSize) - 1.0f;

                    float solidAngle = ComputeCubemapTexelSolidAngle(x0, y0, x1, y1);
                    Vector3 direction = CubemapFaceDirection(face, faceU, faceV);
                    EvaluateSecondOrderShBasis(direction, basis);

                    int pixelIndex = ((y * sampleSize) + x) * 4;
                    Vector3 radiance = new(faceData[pixelIndex], faceData[pixelIndex + 1], faceData[pixelIndex + 2]);

                    for (int i = 0; i < coefficients.Length; i++)
                        coefficients[i] += radiance * (basis[i] * solidAngle);
                }
            }
        }

        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);

        // Lambertian diffuse irradiance is just the SH-projected radiance convolved
        // with the cosine kernel. Each SH band gets a scalar kernel factor.
        coefficients[0] *= MathF.PI;
        coefficients[1] *= (2.0f * MathF.PI / 3.0f);
        coefficients[2] *= (2.0f * MathF.PI / 3.0f);
        coefficients[3] *= (2.0f * MathF.PI / 3.0f);
        coefficients[4] *= (MathF.PI / 4.0f);
        coefficients[5] *= (MathF.PI / 4.0f);
        coefficients[6] *= (MathF.PI / 4.0f);
        coefficients[7] *= (MathF.PI / 4.0f);
        coefficients[8] *= (MathF.PI / 4.0f);

        return coefficients;
    }

    private static int ChooseDiffuseShProjectionMipLevel(int envCubemapResolution, int maxMipLevel)
    {
        int level = 0;
        int size = envCubemapResolution;
        while (size > DiffuseShProjectionFaceSize && level < maxMipLevel)
        {
            size >>= 1;
            level++;
        }

        return level;
    }

    private static float ComputeCubemapTexelSolidAngle(float x0, float y0, float x1, float y1)
    {
        float area = CubemapAreaElement(x0, y0) -
                     CubemapAreaElement(x0, y1) -
                     CubemapAreaElement(x1, y0) +
                     CubemapAreaElement(x1, y1);

        return MathF.Abs(area);
    }

    private static float CubemapAreaElement(float x, float y)
    {
        return MathF.Atan2(x * y, MathF.Sqrt((x * x) + (y * y) + 1.0f));
    }

    private static Vector3 CubemapFaceDirection(int face, float u, float v)
    {
        Vector3 direction = face switch
        {
            0 => new Vector3(1.0f, v, -u),
            1 => new Vector3(-1.0f, v, u),
            2 => new Vector3(u, 1.0f, -v),
            3 => new Vector3(u, -1.0f, v),
            4 => new Vector3(u, v, 1.0f),
            _ => new Vector3(-u, v, -1.0f),
        };

        return Vector3.Normalize(direction);
    }

    private static void EvaluateSecondOrderShBasis(Vector3 direction, Span<float> basis)
    {
        float x = direction.X;
        float y = direction.Y;
        float z = direction.Z;

        basis[0] = 0.282095f;
        basis[1] = 0.488603f * y;
        basis[2] = 0.488603f * z;
        basis[3] = 0.488603f * x;
        basis[4] = 1.092548f * x * y;
        basis[5] = 1.092548f * y * z;
        basis[6] = 0.315392f * ((3.0f * z * z) - 1.0f);
        basis[7] = 1.092548f * x * z;
        basis[8] = 0.546274f * ((x * x) - (y * y));
    }

    public void Dispose()
    {
        _equirectToCubemapShader?.Dispose();
        _cubemapDownsampleShader?.Dispose();
        _irradianceShader?.Dispose();
        _prefilterShader?.Dispose();
        _brdfLutShader?.Dispose();

        if (_cubeVAO != 0) { try { _gl.DeleteVertexArray(_cubeVAO); } catch { } _cubeVAO = 0; }
        if (_cubeVBO != 0) { try { _gl.DeleteBuffer(_cubeVBO); } catch { } _cubeVBO = 0; }

        if (_emptyVao != 0) { try { _gl.DeleteVertexArray(_emptyVao); } catch { } _emptyVao = 0; }
    }
}
