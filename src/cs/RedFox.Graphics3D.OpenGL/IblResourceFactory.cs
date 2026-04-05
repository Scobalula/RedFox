using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed unsafe class IblResourceFactory : IDisposable
{
    private readonly int _maxTextureSize;

    private GL? _gl;
    private GLShader? _equirectToCubemapShader;
    private GLShader? _cubemapDownsampleShader;
    private GLShader? _irradianceShader;
    private GLShader? _prefilterShader;
    private GLShader? _brdfLutShader;
    private uint _cubeVAO;
    private uint _cubeVBO;
    private uint _emptyVao;
    private bool _initialized;

    private GLEnvironmentResources? _resources;
    private bool _computed;

    public const int IrradianceSize = 32;
    public const int PrefilterSize = 256;
    public const int BrdfLutSize = 256;
    private const int DiffuseShProjectionFaceSize = 64;

    public static int PrefilterMipLevels => ComputeFullMipChainLevelCount(PrefilterSize);
    public bool Computed => _computed;

    public IblResourceFactory(int maxTextureSize)
    {
        _maxTextureSize = maxTextureSize;
    }

    public void Initialize(GL gl)
    {
        _gl = gl;

        (string cv, string _) = ShaderSource.LoadProgram(gl, "cubemap");
        (string _, string ef) = ShaderSource.LoadProgram(gl, "equirect_to_cubemap");
        _equirectToCubemapShader = new GLShader(gl, cv, ef);

        (string dv, string df) = ShaderSource.LoadProgram(gl, "cubemap_downsample");
        _cubemapDownsampleShader = new GLShader(gl, dv, df);

        (string iv, string ir) = ShaderSource.LoadProgram(gl, "irradiance");
        _irradianceShader = new GLShader(gl, iv, ir);

        (string pv, string pf) = ShaderSource.LoadProgram(gl, "prefilter");
        _prefilterShader = new GLShader(gl, pv, pf);

        (string bv, string bf) = ShaderSource.LoadProgram(gl, "brdf_lut");
        _brdfLutShader = new GLShader(gl, bv, bf);

        _cubeVAO = gl.GenVertexArray();
        _cubeVBO = gl.GenBuffer();
        gl.BindVertexArray(_cubeVAO);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _cubeVBO);
        unsafe
        {
            fixed (float* ptr = CubeGeometry.Vertices)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(CubeGeometry.Vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }
        }
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, (void*)0);
        gl.BindVertexArray(0);

        _emptyVao = gl.GenVertexArray();
        _initialized = true;
    }

    public GLEnvironmentResources? ComputeIfNecessary(GLRenderer renderer)
    {
        if (!_initialized || _gl is null)
            return null;

        GLEquirectangularEnvironmentMap? envMap = renderer.EnvironmentMap;
        if (envMap is null || string.IsNullOrWhiteSpace(envMap.SourcePath))
            return _resources;

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
            return _resources;
        }

        var expectedManifest = new EnvironmentCacheManifest
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

        string cacheDirectory = GetCacheDirectory(key);

        if (TryLoadFromCache(renderer, expectedManifest, cacheDirectory))
        {
            _computed = true;
            return _resources;
        }

        if (renderer.ImageTranslatorManager is null || !envMap.EnsureTextureLoaded(renderer.ImageTranslatorManager))
            return _resources;

        expectedManifest.SourcePixelHashHex = envMap.SourcePixelHashHex;

        unsafe
        {
            int* vp = stackalloc int[4];
            _gl.GetInteger(GLEnum.Viewport, vp);
            int savedX = vp[0], savedY = vp[1], savedW = vp[2], savedH = vp[3];
            _gl.GetInteger(GLEnum.DrawFramebufferBinding, out int drawFbo);
            int savedFbo = drawFbo;

            _gl.Enable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);

            var resources = new GLEnvironmentResources(_gl, key, skySize, skyMipLevels, IrradianceSize, PrefilterSize, PrefilterMipLevels);

            ConvertEquirectangularToCubemap(envMap, resources.SkyCubemap, skySize);
            GenerateIrradianceMap(resources.SkyCubemap.TextureId, skySize, resources.IrradianceCubemap.TextureId);
            GeneratePrefilterMap(resources.SkyCubemap.TextureId, skySize, resources.PrefilterCubemap.TextureId);
            resources.BrdfLutTexture = GenerateBrdfLut();

            SaveToCache(expectedManifest, cacheDirectory, resources);
            envMap.ReleaseTexture();

            renderer.SetEnvironmentResources(resources);
            _resources = resources;

            _gl.Viewport(savedX, savedY, (uint)savedW, (uint)savedH);
            _gl.BindFramebuffer(GLEnum.Framebuffer, (uint)savedFbo);
            _gl.Enable(EnableCap.DepthTest);

            _computed = true;
            return _resources;
        }
    }

    public void Dispose()
    {
        _equirectToCubemapShader?.Dispose();
        _cubemapDownsampleShader?.Dispose();
        _irradianceShader?.Dispose();
        _prefilterShader?.Dispose();
        _brdfLutShader?.Dispose();

        if (_gl is not null)
        {
            if (_cubeVAO != 0) { try { _gl.DeleteVertexArray(_cubeVAO); } catch { } _cubeVAO = 0; }
            if (_cubeVBO != 0) { try { _gl.DeleteBuffer(_cubeVBO); } catch { } _cubeVBO = 0; }
            if (_emptyVao != 0) { try { _gl.DeleteVertexArray(_emptyVao); } catch { } _emptyVao = 0; }
        }

        _initialized = false;
    }

    private static int ComputeFullMipChainLevelCount(int baseSize)
    {
        int levels = 1;
        int size = Math.Max(baseSize, 1);
        while (size > 1) { size >>= 1; levels++; }
        return levels;
    }

    private static string GetCacheDirectory(string key)
    {
        string cacheBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EnvironmentCache");
        Directory.CreateDirectory(cacheBase);
        return Path.Combine(cacheBase, key);
    }

    private bool TryLoadFromCache(GLRenderer renderer, EnvironmentCacheManifest expected, string cacheDirectory)
    {
        if (_gl is null) return false;
        try
        {
            string manifestPath = Path.Combine(cacheDirectory, "manifest.json");
            if (!File.Exists(manifestPath))
                return false;

            EnvironmentCacheManifest? loaded = JsonSerializer.Deserialize<EnvironmentCacheManifest>(File.ReadAllText(manifestPath));
            if (!ManifestMatches(loaded, expected))
                return false;

            var resources = new GLEnvironmentResources(
                _gl, expected.Key, expected.SkySize, expected.SkyMipLevels,
                expected.IrradianceSize, expected.PrefilterSize, expected.PrefilterMipLevels);

            string skyPath = Path.Combine(cacheDirectory, "sky.foxmap");
            string irrPath = Path.Combine(cacheDirectory, "irradiance.foxmap");
            string prePath = Path.Combine(cacheDirectory, "prefilter.foxmap");
            string brdfPath = Path.Combine(cacheDirectory, "brdf_lut.dat");

            bool skyOk = GLCubemap.CacheExists(skyPath, expected.SkySize, expected.SkyMipLevels) &&
                         resources.SkyCubemap.LoadFromFile(skyPath);
            bool irrOk = GLCubemap.CacheExists(irrPath, expected.IrradianceSize, 1) &&
                         resources.IrradianceCubemap.LoadFromFile(irrPath);
            bool preOk = GLCubemap.CacheExists(prePath, expected.PrefilterSize, expected.PrefilterMipLevels) &&
                         resources.PrefilterCubemap.LoadFromFile(prePath);

            if (!skyOk || !irrOk || !preOk) { resources.Dispose(); return false; }

            if (File.Exists(brdfPath))
                resources.BrdfLutTexture = LoadBrdfLut(brdfPath);
            else
                resources.BrdfLutTexture = GenerateBrdfLut();

            renderer.SetEnvironmentResources(resources);
            _resources = resources;
            return true;
        }
        catch { return false; }
    }

    private static bool ManifestMatches(EnvironmentCacheManifest? loaded, EnvironmentCacheManifest expected)
    {
        return loaded is not null &&
               loaded.CacheVersion == expected.CacheVersion &&
               string.Equals(loaded.Key, expected.Key, StringComparison.Ordinal) &&
               string.Equals(loaded.SourcePath, expected.SourcePath, StringComparison.OrdinalIgnoreCase) &&
               loaded.SourceLastWriteTimeUtcTicks == expected.SourceLastWriteTimeUtcTicks &&
               loaded.FlipY == expected.FlipY &&
               loaded.SkySize == expected.SkySize &&
               loaded.SkyMipLevels == expected.SkyMipLevels &&
               loaded.IrradianceSize == expected.IrradianceSize &&
               loaded.PrefilterSize == expected.PrefilterSize &&
               loaded.PrefilterMipLevels == expected.PrefilterMipLevels &&
               loaded.BrdfLutSize == expected.BrdfLutSize;
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
            File.WriteAllText(Path.Combine(cacheDirectory, "manifest.json"), JsonSerializer.Serialize(expected));
        }
        catch { }
    }

    public unsafe void ConvertEquirectangularToCubemap(GLEquirectangularEnvironmentMap envMap, GLCubemap destination, int size)
    {
        if (_gl is null) return;
        using GLCubemap sourceCubemap = new(_gl);
        sourceCubemap.Create(size, 1, useMipmaps: false);

        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24, (uint)size, (uint)size);

        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();

        _equirectToCubemapShader!.Use();
        _equirectToCubemapShader.SetUniform("uProjection", captureProjection);
        envMap.TextureHandle!.Bind(0);
        _equirectToCubemapShader.SetUniform("uEquirectangularMap", 0);
        _equirectToCubemapShader.SetUniform("uFlipY", envMap.EffectiveFlipY);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, captureRBO);
        _gl.Viewport(0, 0, (uint)size, (uint)size);
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        _gl.BindVertexArray(_cubeVAO);

        for (int face = 0; face < 6; face++)
        {
            _equirectToCubemapShader.SetUniform("uView", captureViews[face]);
            TextureTarget target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, target, sourceCubemap.TextureId, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, target, destination.TextureId, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
        }

        GenerateSkyMipChain(sourceCubemap.TextureId, destination, size);

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);
    }

    private unsafe void GenerateSkyMipChain(uint sourceCubemap, GLCubemap destination, int size)
    {
        if (_gl is null || destination.MipLevels <= 1) return;
        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();

        _cubemapDownsampleShader!.Use();
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

    private unsafe void GenerateIrradianceMap(uint envCubemap, int envCubemapResolution, uint destinationCubemap)
    {
        if (_gl is null) return;
        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24, (uint)IrradianceSize, (uint)IrradianceSize);

        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();
        Vector3[] shCoefficients = ComputeDiffuseIrradianceShCoefficients(envCubemap, envCubemapResolution);

        _irradianceShader!.Use();
        _irradianceShader.SetUniform("uProjection", captureProjection);
        for (int i = 0; i < shCoefficients.Length; i++)
            _irradianceShader.SetUniform($"uShCoefficients[{i}]", shCoefficients[i]);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, captureRBO);
        _gl.Viewport(0, 0, (uint)IrradianceSize, (uint)IrradianceSize);
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        _gl.BindVertexArray(_cubeVAO);

        for (int face = 0; face < 6; face++)
        {
            _irradianceShader.SetUniform("uView", captureViews[face]);
            var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, target, destinationCubemap, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);
    }

    private unsafe void GeneratePrefilterMap(uint envCubemap, int envCubemapResolution, uint destinationCubemap)
    {
        if (_gl is null) return;
        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();

        _prefilterShader!.Use();
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
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24, (uint)mipWidth, (uint)mipHeight);
            _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, captureRBO);
            _gl.Viewport(0, 0, (uint)mipWidth, (uint)mipHeight);
            _prefilterShader.SetUniform("uRoughness", roughness);

            for (int face = 0; face < 6; face++)
            {
                _prefilterShader.SetUniform("uView", captureViews[face]);
                var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
                _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, target, destinationCubemap, level);
                _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
            }
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);
    }

    private unsafe uint GenerateBrdfLut()
    {
        if (_gl is null) return 0;
        uint fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(GLEnum.Framebuffer, fbo);

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

        _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0, GLEnum.Texture2D, brdfTexture, 0);
        _gl.Viewport(0, 0, (uint)BrdfLutSize, (uint)BrdfLutSize);

        _brdfLutShader!.Use();
        _gl.Disable(EnableCap.DepthTest);
        _gl.BindVertexArray(_emptyVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        _gl.BindVertexArray(0);
        _gl.Enable(EnableCap.DepthTest);

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteFramebuffer(fbo);
        return brdfTexture;
    }

    private unsafe void SaveBrdfLut(string path, uint textureId)
    {
        if (_gl is null) return;
        float[] data = new float[BrdfLutSize * BrdfLutSize * 4];
        fixed (float* ptr = data)
        {
            _gl.BindTexture(TextureTarget.Texture2D, textureId);
            _gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.Float, ptr);
        }
        File.WriteAllBytes(path, System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan()).ToArray());
    }

    private unsafe uint LoadBrdfLut(string path)
    {
        if (_gl is null) return 0;
        byte[] data = File.ReadAllBytes(path);
        int expectedFloats = BrdfLutSize * BrdfLutSize * 4;
        if (data.Length != expectedFloats * sizeof(float))
            return GenerateBrdfLut();

        uint brdfTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, brdfTexture);
        fixed (byte* ptr = data)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba32f, BrdfLutSize, BrdfLutSize, 0,
                PixelFormat.Rgba, PixelType.Float, ptr);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return brdfTexture;
    }

    private static Matrix4x4[] GetCaptureViews() =>
    [
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)),
        Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)),
    ];

    private unsafe Vector3[] ComputeDiffuseIrradianceShCoefficients(uint envCubemap, int envCubemapResolution)
    {
        if (_gl is null) return new Vector3[9];
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
        while (size > DiffuseShProjectionFaceSize && level < maxMipLevel) { size >>= 1; level++; }
        return level;
    }

    private static float ComputeCubemapTexelSolidAngle(float x0, float y0, float x1, float y1)
        => MathF.Abs(CubemapAreaElement(x0, y0) - CubemapAreaElement(x0, y1) - CubemapAreaElement(x1, y0) + CubemapAreaElement(x1, y1));

    private static float CubemapAreaElement(float x, float y)
        => MathF.Atan2(x * y, MathF.Sqrt((x * x) + (y * y) + 1.0f));

    private static Vector3 CubemapFaceDirection(int face, float u, float v) => Vector3.Normalize(face switch
    {
        0 => new Vector3(1.0f, v, -u),
        1 => new Vector3(-1.0f, v, u),
        2 => new Vector3(u, 1.0f, -v),
        3 => new Vector3(u, -1.0f, v),
        4 => new Vector3(u, v, 1.0f),
        _ => new Vector3(-u, v, -1.0f),
    });

    private static void EvaluateSecondOrderShBasis(Vector3 d, Span<float> basis)
    {
        float x = d.X, y = d.Y, z = d.Z;
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
}
