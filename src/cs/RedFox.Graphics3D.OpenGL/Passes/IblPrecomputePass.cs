using System.IO;
using System.Numerics;
using RedFox.Graphics3D.OpenGL.Shaders;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL.Passes;

/// <summary>
/// Precomputes IBL data following the learnopengl.com PBR pipeline:
///   1. Equirectangular → cubemap conversion
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
    private GLShader _irradianceShader = null!;
    private GLShader _prefilterShader = null!;
    private GLShader _brdfLutShader = null!;

    private uint _cubeVAO;
    private uint _cubeVBO;

    private GLCubemap _irradianceCubemap = null!;
    private GLCubemap _prefilterCubemap = null!;
    private uint _brdfLutTexture;

    private bool _initialized;
    private bool _computed;

    public const int IrradianceSize = 32;
    public const int PrefilterSize = 256;
    public const int PrefilterMipLevels = 8;
    public const int BrdfLutSize = 256;

    public string Name => "IBL Precompute";
    public bool Enabled { get; set; } = true;
    public bool Computed => _computed;
    public uint IrradianceCubemap => _irradianceCubemap.TextureId;
    public uint PrefilterCubemap => _prefilterCubemap.TextureId;
    public uint BrdfLutTexture => _brdfLutTexture;

    /// <summary>
    /// Maximum mip level index of the prefilter cubemap (used in textureLod).
    /// </summary>
    public float PrefilterMaxMipLevel => PrefilterMipLevels - 1;

    private string? _cacheDirectory;

    public unsafe void Initialize(GLRenderer renderer)
    {
        _gl = renderer.GL;

        // Load shaders
        (string cv, string _) = ShaderSource.LoadProgram(_gl, "cubemap");
        (string _, string ef) = ShaderSource.LoadProgram(_gl, "equirect_to_cubemap");
        _equirectToCubemapShader = new GLShader(_gl, cv, ef);

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

        // Create cubemaps
        _irradianceCubemap = new GLCubemap(_gl);
        _prefilterCubemap = new GLCubemap(_gl);
        _irradianceCubemap.Create(IrradianceSize);
        _prefilterCubemap.Create(PrefilterSize, PrefilterMipLevels, useMipmaps: true);

        _initialized = true;
    }

    public unsafe void Render(GLRenderer renderer, Scene scene, float deltaTime)
    {
        if (!_initialized || !Enabled || _computed) return;
        if (renderer.EnvironmentMap?.TextureHandle is null) return;

        string envMapPath = "default"; // Will be set below
        _cacheDirectory = GetCacheDirectory(renderer, out envMapPath);

        // Check cache
        bool loadedFromCache = TryLoadFromCache();

        if (!loadedFromCache)
        {
            // Save viewport
            int* vp = stackalloc int[4];
            _gl.GetInteger(GLEnum.Viewport, vp);
            int savedX = vp[0], savedY = vp[1], savedW = vp[2], savedH = vp[3];
            int savedFbo = 0;
            _gl.GetInteger(GLEnum.DrawFramebufferBinding, out int drawFbo);
            savedFbo = drawFbo;

            // Disable depth test and culling for cubemap rendering
            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);

            // 1. Convert equirectangular → cubemap (intermediate, not cached)
            uint envCubemap = ConvertEquirectangularToCubemap(renderer, savedW, savedH);

            // 2. Generate irradiance cubemap
            GenerateIrradianceMap(envCubemap, savedW, savedH);

            // 3. Generate prefiltered environment cubemap
            GeneratePrefilterMap(envCubemap, savedW, savedH);

            // 4. Generate BRDF LUT
            GenerateBrdfLut(savedW, savedH);

            // Clean up intermediate cubemap
            _gl.DeleteTexture(envCubemap);

            // Save to cache
            SaveToCache();

            // Restore state
            _gl.Viewport(savedX, savedY, (uint)savedW, (uint)savedH);
            _gl.BindFramebuffer(GLEnum.Framebuffer, (uint)savedFbo);
            _gl.Enable(EnableCap.DepthTest);
            if (renderer.EnableBackFaceCulling)
                _gl.Enable(EnableCap.CullFace);
            else
                _gl.Disable(EnableCap.CullFace);
        }

        _computed = true;
        Enabled = false;
    }

    // ------------------------------------------------------------------
    // Cache
    // ------------------------------------------------------------------

    private string GetCacheDirectory(GLRenderer renderer, out string envMapPath)
    {
        envMapPath = "";
        // We need the original env map file path. Store it when available.
        // Since GLEquirectangularEnvironmentMap doesn't expose it, we'll compute a hash.
        var cacheBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IBLCache");
        Directory.CreateDirectory(cacheBase);
        return cacheBase;
    }

    private bool TryLoadFromCache()
    {
        if (_cacheDirectory == null) return false;

        string irrPath = Path.Combine(_cacheDirectory, "irradiance.foxmap");
        string prePath = Path.Combine(_cacheDirectory, "prefilter.foxmap");
        string brdfPath = Path.Combine(_cacheDirectory, "brdf_lut.dat");

        bool irrOk = GLCubemap.CacheExists(irrPath, IrradianceSize, 1) &&
                     _irradianceCubemap.LoadFromFile(irrPath);
        bool preOk = GLCubemap.CacheExists(prePath, PrefilterSize, PrefilterMipLevels) &&
                     _prefilterCubemap.LoadFromFile(prePath);

        if (irrOk && preOk)
        {
            // Load BRDF LUT
            if (File.Exists(brdfPath))
            {
                LoadBrdfLut(brdfPath);
            }
            else
            {
                GenerateBrdfLut(0, 0);
                SaveBrdfLut(brdfPath);
            }
            return true;
        }
        return false;
    }

    private void SaveToCache()
    {
        if (_cacheDirectory == null) return;

        Directory.CreateDirectory(_cacheDirectory);
        try
        {
            _irradianceCubemap.SaveToFile(Path.Combine(_cacheDirectory, "irradiance.foxmap"));
            _prefilterCubemap.SaveToFile(Path.Combine(_cacheDirectory, "prefilter.foxmap"));
            SaveBrdfLut(Path.Combine(_cacheDirectory, "brdf_lut.dat"));
        }
        catch
        {
            // Cache save is non-critical
        }
    }

    // ------------------------------------------------------------------
    // Equirectangular → Cubemap conversion
    // ------------------------------------------------------------------

    private unsafe uint ConvertEquirectangularToCubemap(GLRenderer renderer, int vpW, int vpH)
    {
        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24, (uint)1024, (uint)1024);

        uint captureCubemap = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.TextureCubeMap, captureCubemap);

        // Allocate cubemap faces at 1024x1024
        for (int face = 0; face < 6; face++)
        {
            var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            float[] emptyData = new float[1024 * 1024 * 4];
            fixed (float* ptr = emptyData)
            {
                _gl.TexImage2D(target, 0, InternalFormat.Rgba32f, 1024u, 1024u, 0, PixelFormat.Rgba, PixelType.Float, ptr);
            }
        }
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);

        // Projection for cubemap faces: 90° FOV, 1:1 aspect
        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);

        Matrix4x4[] captureViews =
        [
            Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)), // +X
            Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(-1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)), // -X
            Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f,  1.0f,  0.0f), new Vector3(0.0f,  0.0f,  1.0f)), // +Y
            Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f, -1.0f,  0.0f), new Vector3(0.0f,  0.0f, -1.0f)), // -Y
            Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f,  0.0f,  1.0f), new Vector3(0.0f, -1.0f,  0.0f)), // +Z
            Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3( 0.0f,  0.0f, -1.0f), new Vector3(0.0f, -1.0f,  0.0f)), // -Z
        ];

        _equirectToCubemapShader.Use();
        _equirectToCubemapShader.SetUniform("uProjection", captureProjection);
        renderer.EnvironmentMap.TextureHandle!.Bind(0);
        _equirectToCubemapShader.SetUniform("uEquirectangularMap", 0);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
            GLEnum.Renderbuffer, captureRBO);
        _gl.Viewport(0, 0, 1024u, 1024u);
        _gl.Clear(ClearBufferMask.DepthBufferBit);

        _gl.BindVertexArray(_cubeVAO);

        for (int face = 0; face < 6; face++)
        {
            _equirectToCubemapShader.SetUniform("uView", captureViews[face]);
            var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
                target, captureCubemap, 0);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, CubeGeometry.VertexCount);
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteRenderbuffer(captureRBO);
        _gl.DeleteFramebuffer(captureFBO);

        return captureCubemap;
    }

    // ------------------------------------------------------------------
    // Irradiance map generation
    // ------------------------------------------------------------------

    private unsafe void GenerateIrradianceMap(uint envCubemap, int vpW, int vpH)
    {
        uint captureFBO = _gl.GenFramebuffer();
        uint captureRBO = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, captureRBO);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, GLEnum.DepthComponent24,
            (uint)IrradianceSize, (uint)IrradianceSize);

        Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2.0f, 1.0f, 0.1f, 10.0f);
        Matrix4x4[] captureViews = GetCaptureViews();

        _irradianceShader.Use();
        _irradianceShader.SetUniform("uProjection", captureProjection);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.TextureCubeMap, envCubemap);
        _irradianceShader.SetUniform("uEnvironmentMap", 0);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
            GLEnum.Renderbuffer, captureRBO);
        _gl.Viewport(0, 0, (uint)IrradianceSize, (uint)IrradianceSize);
        _gl.Clear(ClearBufferMask.DepthBufferBit);

        _gl.BindVertexArray(_cubeVAO);

        for (int face = 0; face < 6; face++)
        {
            _irradianceShader.SetUniform("uView", captureViews[face]);
            var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
                target, _irradianceCubemap.TextureId, 0);
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

    private unsafe void GeneratePrefilterMap(uint envCubemap, int vpW, int vpH)
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
        _prefilterShader.SetUniform("uEnvMapResolution", (float)PrefilterSize);

        _gl.BindFramebuffer(GLEnum.Framebuffer, captureFBO);
        _gl.BindVertexArray(_cubeVAO);

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
            _gl.Clear(ClearBufferMask.DepthBufferBit);

            _prefilterShader.SetUniform("uRoughness", roughness);

            for (int face = 0; face < 6; face++)
            {
                _prefilterShader.SetUniform("uView", captureViews[face]);
                var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
                _gl.FramebufferTexture2D(GLEnum.Framebuffer, GLEnum.ColorAttachment0,
                    target, _prefilterCubemap.TextureId, level);
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

    private unsafe void GenerateBrdfLut(int vpW, int vpH)
    {
        uint fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(GLEnum.Framebuffer, fbo);

        // Create BRDF LUT texture
        _brdfLutTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _brdfLutTexture);
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
            GLEnum.Texture2D, _brdfLutTexture, 0);
        _gl.Viewport(0, 0, (uint)BrdfLutSize, (uint)BrdfLutSize);

        _brdfLutShader.Use();
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

        _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
        _gl.DeleteFramebuffer(fbo);
    }

    private void SaveBrdfLut(string path)
    {
        float[] data = new float[BrdfLutSize * BrdfLutSize * 4];
        unsafe
        {
            fixed (float* ptr = data)
            {
                _gl.BindTexture(TextureTarget.Texture2D, _brdfLutTexture);
                _gl.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.Float, ptr);
            }
        }
        File.WriteAllBytes(path, System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan()).ToArray());
    }

    private void LoadBrdfLut(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int expectedFloats = BrdfLutSize * BrdfLutSize * 4;
        if (data.Length != expectedFloats * sizeof(float))
        {
            GenerateBrdfLut(0, 0);
            return;
        }

        _brdfLutTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _brdfLutTexture);
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

    public void Dispose()
    {
        _equirectToCubemapShader?.Dispose();
        _irradianceShader?.Dispose();
        _prefilterShader?.Dispose();
        _brdfLutShader?.Dispose();

        if (_cubeVAO != 0) { try { _gl.DeleteVertexArray(_cubeVAO); } catch { } _cubeVAO = 0; }
        if (_cubeVBO != 0) { try { _gl.DeleteBuffer(_cubeVBO); } catch { } _cubeVBO = 0; }

        _irradianceCubemap?.Dispose();
        _prefilterCubemap?.Dispose();

        if (_brdfLutTexture != 0) { try { _gl.DeleteTexture(_brdfLutTexture); } catch { } _brdfLutTexture = 0; }
    }
}
