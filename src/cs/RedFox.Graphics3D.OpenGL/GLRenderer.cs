using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D.OpenGL.Passes;
using RedFox.Graphics3D.OpenGL.Shaders;
using RedFox.Graphics3D.OpenGL.Viewing;
using Silk.NET.OpenGL;

namespace RedFox.Graphics3D.OpenGL;

public sealed class GLRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly List<IRenderPass> _prepasses = [];
    private readonly List<IRenderPass> _renderPasses = [];
    private readonly List<IRenderPass> _postpasses = [];

    private GLEquirectangularEnvironmentMap? _environmentMap;
    private GLEnvironmentResources? _environmentResources;
    private IblResourceFactory? _iblFactory;
    private MultisampleFramebufferObject? _msaaFramebuffer;
    private GlTextureUploader? _textureUploader;

    private bool _isInitialized;
    private int _maxTextureSize = 2048;
    private int _maxMsaaSamples = 1;
    private int _outputWidth;
    private int _outputHeight;

    private ImageTranslatorManager? _imageTranslatorManager;

    private Vector3? _dominantLightDirection;
    private Vector3 _dominantLightColor = Vector3.One;
    private float _dominantLightIntensity = 1.0f;
    private GLEnvironmentResources? _lastDominantLightEnvResources;

    public GLRenderer(GL gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        Settings = new RenderSettings();
    }

    public GL GL => _gl;
    public RenderSettings Settings { get; }

    public GLEnvironmentResources? EnvironmentResources => _environmentResources;

    public Vector3? DominantLightDirection => _dominantLightDirection;
    public Vector3 DominantLightColor => _dominantLightColor;
    public float DominantLightIntensity => _dominantLightIntensity;

    public ImageTranslatorManager? ImageTranslatorManager
    {
        get => _imageTranslatorManager;
        set
        {
            _imageTranslatorManager = value;
            _textureUploader = value is not null ? new GlTextureUploader(_gl, value) : null;
        }
    }

    public GLEquirectangularEnvironmentMap? EnvironmentMap
    {
        get => _environmentMap;
        set
        {
            if (ReferenceEquals(_environmentMap, value))
                return;

            _environmentMap?.Dispose();
            _environmentMap = value;
            SetEnvironmentResources(null);
        }
    }

    public EnvironmentMapFlipMode EnvironmentMapFlipMode { get; set; } = EnvironmentMapFlipMode.Auto;

    public void Initialize()
    {
        if (_isInitialized)
            return;

        Settings.IsOpenGles = ShaderSource.GetProfile(_gl) == ShaderProfile.OpenGles;
        _gl.GetInteger(GLEnum.MaxTextureSize, out _maxTextureSize);
        _gl.GetInteger(GLEnum.MaxSamples, out _maxMsaaSamples);
        _maxTextureSize = Math.Max(_maxTextureSize, 1);
        _maxMsaaSamples = Math.Max(_maxMsaaSamples, 1);

        _iblFactory = new IblResourceFactory(_maxTextureSize);
        _iblFactory.Initialize(_gl);
        _textureUploader ??= _imageTranslatorManager is not null
            ? new GlTextureUploader(_gl, _imageTranslatorManager)
            : null;

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Multisample);
        if (!Settings.IsOpenGles)
            _gl.Enable(GLEnum.TextureCubeMapSeamless);
        _gl.FrontFace(FrontFaceDirection.Ccw);

        if (GetPassesByPhase(PassPhase.Pass).Count == 0)
        {
            AddPass(new EnvironmentMapPass());
            AddPass(new GeometryPass());
            AddPass(new BonePass());
        }

        if (GetPassesByPhase(PassPhase.Prepass).Count == 0)
        {
            AddPass(new MeshDeformPass());
            AddPass(new ShadowMapPass());
        }

        if (GetPassesByPhase(PassPhase.Postpass).Count == 0)
            AddPass(new GridPass());

        foreach (IRenderPass pass in GetAllPasses())
            pass.Initialize(this);

        _isInitialized = true;
        RecreateMultisampleFramebuffer();
    }

    public void AddPass(IRenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        GetPassesByPhase(pass.Phase).Add(pass);

        if (_isInitialized)
            pass.Initialize(this);
    }

    public bool RemovePass(string name)
    {
        foreach (List<IRenderPass> list in GetAllPassLists())
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Name.Equals(name, StringComparison.Ordinal))
                    continue;

                list[i].Dispose();
                list.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public T? GetPass<T>() where T : class, IRenderPass
    {
        foreach (List<IRenderPass> list in GetAllPassLists())
        {
            foreach (IRenderPass pass in list)
            {
                if (pass is T typed)
                    return typed;
            }
        }

        return null;
    }

    public IReadOnlyList<IRenderPass> GetAllPasses()
    {
        List<IRenderPass> all = new(_prepasses.Count + _renderPasses.Count + _postpasses.Count);
        all.AddRange(_prepasses);
        all.AddRange(_renderPasses);
        all.AddRange(_postpasses);
        return all;
    }

    public void SetOutputSize(int width, int height)
    {
        width = Math.Max(width, 0);
        height = Math.Max(height, 0);

        if (_outputWidth == width && _outputHeight == height)
            return;

        _outputWidth = width;
        _outputHeight = height;

        if (_isInitialized)
            RecreateMultisampleFramebuffer();
    }

    public MeshRenderHandle? GetOrCreateMeshHandle(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.GraphicsHandle is MeshRenderHandle existing)
            return existing;

        var handle = new MeshRenderHandle(mesh);
        var deformPass = GetPass<MeshDeformPass>();
        handle.SetComputeAvailable(deformPass?.ComputeAvailable ?? false);
        handle.Initialize(_gl);
        mesh.GraphicsHandle = handle;
        return handle;
    }

    public SkeletonRenderHandle? GetOrCreateSkeletonHandle(Skeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        if (skeleton.GraphicsHandle is SkeletonRenderHandle existing)
        {
            if (!existing.NeedsRebuild())
                return existing;

            existing.Dispose();
        }

        var handle = new SkeletonRenderHandle(skeleton, _maxTextureSize);
        handle.Initialize(_gl);
        skeleton.GraphicsHandle = handle;
        return handle;
    }

    public GLTextureHandle? GetOrCreateTextureHandle(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (texture.GraphicsHandle is GLTextureHandle existing)
            return existing;

        GLTextureHandle? uploaded = _textureUploader?.Upload(texture);
        texture.GraphicsHandle = uploaded;
        return uploaded;
    }

    public void UnloadMesh(Mesh mesh)
    {
        if (mesh.GraphicsHandle is MeshRenderHandle handle)
        {
            handle.Dispose();
            mesh.GraphicsHandle = null;
        }
    }

    public void UnloadTexture(Texture texture)
    {
        if (texture.GraphicsHandle is GLTextureHandle handle)
        {
            handle.Dispose();
            texture.GraphicsHandle = null;
        }
    }

    public void UnloadSkeleton(Skeleton skeleton)
    {
        if (skeleton.GraphicsHandle is SkeletonRenderHandle handle)
        {
            handle.Dispose();
            skeleton.GraphicsHandle = null;
        }
    }

    public unsafe void Render(Scene scene, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(scene);

        int* viewport = stackalloc int[4];
        _gl.GetInteger(GLEnum.Viewport, viewport);
        int savedViewportX = viewport[0];
        int savedViewportY = viewport[1];
        int savedViewportWidth = viewport[2];
        int savedViewportHeight = viewport[3];

        _gl.GetInteger(GLEnum.DrawFramebufferBinding, out int savedDrawFramebuffer);
        _gl.GetInteger(GLEnum.ReadFramebufferBinding, out int savedReadFramebuffer);

        EnsureMultisampleFramebuffer(savedViewportWidth, savedViewportHeight);

        if (_msaaFramebuffer is not null)
        {
            _msaaFramebuffer.BindForRendering();
            _gl.Viewport(0, 0, (uint)_msaaFramebuffer.Width, (uint)_msaaFramebuffer.Height);
        }

        RendererColor bg = Settings.BackgroundColor;
        _gl.ClearColor(bg.R, bg.G, bg.B, bg.A);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (Settings.EnableBackFaceCulling)
        {
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(GLEnum.Back);
        }
        else
        {
            _gl.Disable(EnableCap.CullFace);
        }

        if (_iblFactory is not null && _environmentMap is not null)
        {
            GLEnvironmentResources? iblResources = _iblFactory.ComputeIfNecessary(this);
            if (iblResources is not null)
                SetEnvironmentResources(iblResources);
        }

        UpdateDominantLight();

        ExecutePasses(_prepasses, scene, deltaTime);
        ExecutePasses(_renderPasses, scene, deltaTime);
        ExecutePasses(_postpasses, scene, deltaTime);

        if (_msaaFramebuffer is not null)
        {
            _gl.BindFramebuffer(GLEnum.ReadFramebuffer, _msaaFramebuffer.FramebufferId);
            _gl.BindFramebuffer(GLEnum.DrawFramebuffer, (uint)savedDrawFramebuffer);
            _gl.BlitFramebuffer(
                0, 0, _msaaFramebuffer.Width, _msaaFramebuffer.Height,
                savedViewportX, savedViewportY,
                savedViewportX + savedViewportWidth, savedViewportY + savedViewportHeight,
                (uint)ClearBufferMask.ColorBufferBit, GLEnum.Linear);
        }

        _gl.BindFramebuffer(GLEnum.ReadFramebuffer, (uint)savedReadFramebuffer);
        _gl.BindFramebuffer(GLEnum.DrawFramebuffer, (uint)savedDrawFramebuffer);
        _gl.Viewport(savedViewportX, savedViewportY, (uint)savedViewportWidth, (uint)savedViewportHeight);
    }

    public void Dispose()
    {
        foreach (IRenderPass pass in GetAllPasses())
            pass.Dispose();

        _prepasses.Clear();
        _renderPasses.Clear();
        _postpasses.Clear();

        SetEnvironmentResources(null);

        _environmentMap?.Dispose();
        _environmentMap = null;

        _iblFactory?.Dispose();
        _iblFactory = null;

        _msaaFramebuffer?.Dispose();
        _msaaFramebuffer = null;

    }

    internal void SetEnvironmentResources(GLEnvironmentResources? resources)
    {
        if (ReferenceEquals(_environmentResources, resources))
            return;

        _environmentResources?.Dispose();
        _environmentResources = resources;
        _lastDominantLightEnvResources = null;
    }

    internal void UpdateDominantLight()
    {
        if (!Settings.AutoDetectShadowLight || !Settings.EnableShadows)
            return;

        GLEnvironmentResources? env = _environmentResources;
        if (env is null || env.IrradianceCubemap.TextureId == 0)
        {
            _dominantLightDirection = null;
            return;
        }

        if (ReferenceEquals(env, _lastDominantLightEnvResources))
            return;

        _lastDominantLightEnvResources = env;
        ExtractDominantLightFromCubemap(env);
    }

    private unsafe void ExtractDominantLightFromCubemap(GLEnvironmentResources env)
    {
        if (Settings.IsOpenGles)
        {
            _dominantLightDirection = null;
            return;
        }

        ReadOnlySpan<Vector3> faceDirections =
        [
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitY,
            -Vector3.UnitY,
            Vector3.UnitZ,
            -Vector3.UnitZ,
        ];

        uint cubemapId = env.IrradianceCubemap.TextureId;
        int faceSize = env.IrradianceCubemap.Size;
        int sampleSize = Math.Min(faceSize, 4);

        float[] pixels = new float[sampleSize * sampleSize * 4];
        int mipLevel = 0;
        int actualSize = faceSize;
        while (actualSize > sampleSize && mipLevel < 8)
        {
            mipLevel++;
            actualSize = Math.Max(faceSize >> mipLevel, 1);
        }
        if (actualSize > sampleSize)
        {
            mipLevel = 0;
            actualSize = faceSize;
        }

        pixels = new float[actualSize * actualSize * 4];

        Vector3 bestDir = -Vector3.UnitY;
        float bestLuminance = 0.0f;
        Vector3 weightedColor = Vector3.Zero;
        float totalWeight = 0.0f;

        _gl.BindTexture(TextureTarget.TextureCubeMap, cubemapId);

        for (int face = 0; face < 6; face++)
        {
            var target = (TextureTarget)((int)TextureTarget.TextureCubeMapPositiveX + face);
            fixed (float* ptr = pixels)
            {
                _gl.GetTexImage(target, mipLevel, PixelFormat.Rgba, PixelType.Float, ptr);
            }

            for (int y = 0; y < actualSize; y++)
            {
                for (int x = 0; x < actualSize; x++)
                {
                    int idx = (y * actualSize + x) * 4;
                    float r = pixels[idx];
                    float g = pixels[idx + 1];
                    float b = pixels[idx + 2];
                    float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;

                    Vector3 dir = ComputeCubemapDirection(face, x, y, actualSize);
                    float weight = luminance * luminance;
                    weightedColor += new Vector3(r, g, b) * weight;
                    totalWeight += weight;

                    if (luminance > bestLuminance)
                    {
                        bestLuminance = luminance;
                        bestDir = dir;
                    }
                }
            }
        }

        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);

        if (bestLuminance > 0.001f)
        {
            _dominantLightDirection = Vector3.Normalize(bestDir);
            _dominantLightColor = totalWeight > 0.0f
                ? Vector3.Normalize(weightedColor / totalWeight)
                : Vector3.One;
            _dominantLightIntensity = MathF.Min(bestLuminance, 5.0f);
        }
        else
        {
            _dominantLightDirection = null;
            _dominantLightColor = Vector3.One;
            _dominantLightIntensity = 1.0f;
        }
    }

    private static Vector3 ComputeCubemapDirection(int face, int x, int y, int faceSize)
    {
        float u = (x + 0.5f) / faceSize * 2.0f - 1.0f;
        float v = (y + 0.5f) / faceSize * 2.0f - 1.0f;
        return face switch
        {
            0 => Vector3.Normalize(new Vector3(1.0f, -v, -u)),
            1 => Vector3.Normalize(new Vector3(-1.0f, -v, u)),
            2 => Vector3.Normalize(new Vector3(u, 1.0f, v)),
            3 => Vector3.Normalize(new Vector3(u, -1.0f, -v)),
            4 => Vector3.Normalize(new Vector3(u, -v, 1.0f)),
            _ => Vector3.Normalize(new Vector3(-u, -v, -1.0f)),
        };
    }

    private void ExecutePasses(List<IRenderPass> passes, Scene scene, float deltaTime)
    {
        foreach (IRenderPass pass in passes)
        {
            if (pass.Enabled)
                pass.Render(this, scene, deltaTime);
        }
    }

    private List<IRenderPass> GetPassesByPhase(PassPhase phase) => phase switch
    {
        PassPhase.Prepass => _prepasses,
        PassPhase.Postpass => _postpasses,
        _ => _renderPasses,
    };

    private List<IRenderPass>[] GetAllPassLists() => [_prepasses, _renderPasses, _postpasses];

    private void EnsureMultisampleFramebuffer(int viewportWidth, int viewportHeight)
    {
        if (_outputWidth <= 0 || _outputHeight <= 0)
        {
            _outputWidth = Math.Max(viewportWidth, 0);
            _outputHeight = Math.Max(viewportHeight, 0);
        }

        if (_msaaFramebuffer is not null &&
            _msaaFramebuffer.Width == _outputWidth &&
            _msaaFramebuffer.Height == _outputHeight &&
            _msaaFramebuffer.SampleCount == GetEffectiveRequestedMsaaSamples())
        {
            return;
        }

        RecreateMultisampleFramebuffer();
    }

    private void RecreateMultisampleFramebuffer()
    {
        _msaaFramebuffer?.Dispose();
        _msaaFramebuffer = null;

        int width = _outputWidth;
        int height = _outputHeight;
        int samples = GetEffectiveRequestedMsaaSamples();

        if (!_isInitialized || width <= 0 || height <= 0 || samples <= 1)
            return;

        _msaaFramebuffer = new MultisampleFramebufferObject(_gl);
        _msaaFramebuffer.Initialize(width, height, samples);
    }

    private int GetEffectiveRequestedMsaaSamples()
    {
        if (Settings.RequestedMsaaSamples <= 1)
            return 1;

        return Math.Clamp(Settings.RequestedMsaaSamples, 1, _maxMsaaSamples);
    }
}
