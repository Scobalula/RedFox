using System;
using System.Numerics;
using RedFox.Imaging.Primitives;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Handles;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Provides a GPU-driven scene renderer that draws a scene with a camera.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    private const ImageFormat AntiAliasingColorFormat = ImageFormat.B8G8R8A8Unorm;
    private const ImageFormat AntiAliasingDepthFormat = ImageFormat.D32Float;
    private const int DefaultAntiAliasingSamples = 4;
    private const int MinimumAntiAliasingSamples = 1;
    private const float ShadowBlurRadius = 0.005f;
    private const float WireframeDepthBias = 0.002f;
    private const string WireframeMaterialTypeName = "Wireframe";

    private static readonly RenderFlags[] RenderPhases =
    [
        RenderFlags.Opaque,
        RenderFlags.Transparent,
        RenderFlags.Overlay,
    ];

    private static readonly string[] PreparedMaterialTypeNames =
    [
        MaterialRenderHandle.DefaultMaterialTypeName,
        SkeletonBoneRenderHandle.SkeletonMaterialTypeName,
        GridRenderHandle.GridMaterialTypeName,
        SkyboxRenderHandle.SkyboxMaterialTypeName,
    ];

    private static readonly Comparison<MeshRenderHandle> FrontToBack = static (left, right) => left.ViewDistanceSquared.CompareTo(right.ViewDistanceSquared);
    private static readonly Comparison<MeshRenderHandle> BackToFront = static (left, right) => right.ViewDistanceSquared.CompareTo(left.ViewDistanceSquared);

    private readonly ClearAndStateResetPass _clearAndStateResetPass;
    private readonly DepthPrepass _depthPrepass;
    private readonly AmbientOcclusionPass _ambientOcclusionPass;
    private readonly ICommandList _commandList;
    private readonly BackgroundTextureLoader _textureLoader = new();
    private readonly IGraphicsDevice _graphicsDevice;
    private IReadOnlyList<SceneNode> _sceneTraversalNodes = [];
    private readonly List<(SceneNode Node, IRenderHandle Handle)> _sceneTraversalHandles = [];
    private readonly List<MeshRenderHandle> _opaqueMeshes = [];
    private readonly List<MeshRenderHandle> _transparentMeshes = [];
    private readonly List<MeshRenderHandle> _visibleMeshes = [];
    private readonly List<IRenderHandle> _otherHandles = [];
    private readonly Dictionary<string, IGpuPipelineState> _preparedPipelines = [];
    private RenderFrameContext? _frameContext;
    private IGpuPipelineState? _wireframePipeline;
    private IGpuPipelineState? _normalPipeline;

    private IGpuTexture? _antiAliasingColorTexture;
    private IGpuTexture? _antiAliasingDepthTexture;
    private IGpuRenderTarget? _antiAliasingRenderTarget;
    private int _actualAntiAliasingSamples = MinimumAntiAliasingSamples;
    private int _antiAliasingSamples = DefaultAntiAliasingSamples;
    private int _antiAliasingTargetHeight;
    private int _antiAliasingTargetWidth;
    private int _externalAntiAliasingSamples = MinimumAntiAliasingSamples;
    private bool _disposed;
    private bool _hasPendingUploads;
    private bool _initialized;
    private Scene? _sceneTraversalScene;
    private long _sceneTraversalVersion = -1;
    private int _viewportHeight = 1;
    private int _viewportWidth = 1;
    private int _shadowCount;
    private int _shadowKey;
    private int _shadowMeshesKey;
    private SceneBounds _shadowBounds;
    private EnvironmentLight[]? _shadowLights;
    private Matrix4x4 _shadowSceneAxis;
    private int _staleShadowCount;
    private int _nextShadowIndex;
    private int _shadowMapSize;
    private float _shadowSoftness;

    /// <summary>
    /// Gets the graphics device used by the renderer.
    /// </summary>
    public IGraphicsDevice GraphicsDevice => _graphicsDevice;

    /// <summary>
    /// Gets or sets the clear color applied at frame start.
    /// </summary>
    public Vector4 ClearColor
    {
        get => _clearAndStateResetPass.ClearColor;
        set => _clearAndStateResetPass.ClearColor = value;
    }

    /// <summary>
    /// Gets or sets the ambient color used for renderer lighting.
    /// </summary>
    public Vector3 AmbientColor { get; set; }

    /// <summary>
    /// Gets or sets the fallback light direction used when no enabled scene lights are available.
    /// </summary>
    public Vector3 FallbackLightDirection { get; set; }

    /// <summary>
    /// Gets or sets the fallback light color used when no enabled scene lights are available.
    /// </summary>
    public Vector3 FallbackLightColor { get; set; }

    /// <summary>
    /// Gets or sets the fallback light intensity used when no enabled scene lights are available.
    /// </summary>
    public float FallbackLightIntensity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether view-based lighting is enabled.
    /// </summary>
    public bool UseViewBasedLighting { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether materials are drawn with their base color only, without lighting, shadows or ambient occlusion.
    /// </summary>
    public bool UseFlatShading { get; set; }

    /// <summary>
    /// Gets or sets how mesh wireframes are drawn.
    /// </summary>
    public WireframeMode WireframeMode { get; set; }

    /// <summary>
    /// Gets or sets whether vertex normal direction lines are drawn over meshes.
    /// </summary>
    public bool ShowNormals { get; set; }

    public ShadowQuality ShadowQuality { get; set; } = ShadowQuality.High;

    public float ShadowSoftness { get; set; } = 1.0f;

    public float ShadowStrength { get; set; } = 1.0f;

    public AmbientOcclusionQuality AmbientOcclusionQuality
    {
        get => _ambientOcclusionPass.Quality;
        set => _ambientOcclusionPass.Quality = value;
    }

    public float AmbientOcclusionRadius
    {
        get => _ambientOcclusionPass.Radius;
        set => _ambientOcclusionPass.Radius = value;
    }

    public float AmbientOcclusionIntensity
    {
        get => _ambientOcclusionPass.Intensity;
        set => _ambientOcclusionPass.Intensity = value;
    }

    /// <summary>
    /// Gets or sets the skinning mode used during rendering. Meshes whose <see cref="Skin.SkinningMode"/> is
    /// <see cref="SkinningMode.DualQuaternion"/> always render with dual quaternion skinning.
    /// </summary>
    public SkinningMode SkinningMode { get; set; }

    /// <summary>
    /// Gets or sets the requested multisample anti-aliasing sample count. The default is 4.
    /// </summary>
    public int AntiAliasingSamples
    {
        get => _antiAliasingSamples;
        set
        {
            if (value < MinimumAntiAliasingSamples)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Anti-aliasing samples must be at least 1.");
            }

            if (_antiAliasingSamples == value)
            {
                return;
            }

            _antiAliasingSamples = value;
            ReleaseAntiAliasingResources();
        }
    }

    /// <summary>
    /// Gets the multisample anti-aliasing sample count used by the current render target.
    /// </summary>
    public int ActualAntiAliasingSamples => _actualAntiAliasingSamples;

    public bool HasPendingWork { get; private set; }

    /// <summary>
    /// Gets or sets how many textures are loaded at the same time. Every <see cref="ITextureLoader"/> must be thread-safe when this is above one.
    /// </summary>
    public int TextureLoadConcurrency
    {
        get => _textureLoader.Concurrency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _textureLoader.Concurrency = value;
        }
    }

    /// <summary>
    /// Gets or sets the sample count already provided by the bound default framebuffer.
    /// When greater than one, the renderer uses that framebuffer directly instead of resolving into it.
    /// </summary>
    public int ExternalAntiAliasingSamples
    {
        get => _externalAntiAliasingSamples;
        set
        {
            if (value < MinimumAntiAliasingSamples)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "External anti-aliasing samples must be at least 1.");
            }

            if (_externalAntiAliasingSamples == value)
            {
                return;
            }

            _externalAntiAliasingSamples = value;
            ReleaseAntiAliasingResources();
            if (_externalAntiAliasingSamples > MinimumAntiAliasingSamples)
            {
                _actualAntiAliasingSamples = _externalAntiAliasingSamples;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneRenderer"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that executes rendering work.</param>
    /// <param name="clearColor">The clear color applied at frame start.</param>
    /// <param name="ambientColor">The ambient light contribution.</param>
    /// <param name="fallbackLightDirection">The fallback light direction.</param>
    /// <param name="fallbackLightColor">The fallback light color.</param>
    /// <param name="fallbackLightIntensity">The fallback light intensity.</param>
    /// <param name="useViewBasedLighting">Whether view-based lighting is enabled.</param>
    /// <param name="skinningMode">The active skinning mode.</param>
    public SceneRenderer(IGraphicsDevice graphicsDevice, Vector4 clearColor, Vector3 ambientColor, Vector3 fallbackLightDirection, Vector3 fallbackLightColor, float fallbackLightIntensity, bool useViewBasedLighting, SkinningMode skinningMode)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _commandList = graphicsDevice.CreateCommandList();
        _clearAndStateResetPass = new ClearAndStateResetPass(clearColor);
        _depthPrepass = new DepthPrepass(graphicsDevice, _opaqueMeshes);
        _ambientOcclusionPass = new AmbientOcclusionPass(graphicsDevice);
        AmbientColor = ambientColor;
        FallbackLightDirection = fallbackLightDirection;
        FallbackLightColor = fallbackLightColor;
        FallbackLightIntensity = fallbackLightIntensity;
        UseViewBasedLighting = useViewBasedLighting;
        SkinningMode = skinningMode;
    }

    /// <summary>
    /// Initializes renderer state and GPU resources.
    /// </summary>
    public void Initialize()
    {
        ThrowIfDisposed();
        if (_initialized)
        {
            return;
        }

        _clearAndStateResetPass.Initialize();
        _initialized = true;
    }

    // Builds the built-in material pipelines ahead of the first scene, so their shader compiles happen when the caller chooses.
    // The renderer holds a reference to each until it is disposed, keeping them in the registry's cache for later handles.
    public void PreparePipelines()
    {
        ThrowIfDisposed();
        if (_preparedPipelines.Count > 0 || _graphicsDevice.MaterialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            return;
        }

        foreach (string typeName in PreparedMaterialTypeNames)
        {
            if (_graphicsDevice.MaterialTypes.Contains(typeName))
            {
                _preparedPipelines[typeName] = pipelineProvider.AcquirePipeline(_graphicsDevice, typeName);
            }
        }
    }

    /// <summary>
    /// Resizes the active viewport.
    /// </summary>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    public void Resize(int width, int height)
    {
        ThrowIfDisposed();
        int safeWidth = Math.Max(1, width);
        int safeHeight = Math.Max(1, height);
        if (_viewportWidth != safeWidth || _viewportHeight != safeHeight)
        {
            ReleaseAntiAliasingResources();
        }

        _viewportWidth = safeWidth;
        _viewportHeight = safeHeight;
        _clearAndStateResetPass.Resize(_viewportWidth, _viewportHeight);
        _depthPrepass.Resize(_viewportWidth, _viewportHeight);
        _ambientOcclusionPass.Resize(_viewportWidth, _viewportHeight);
    }

    /// <summary>
    /// Renders the provided scene from the provided camera view.
    /// </summary>
    /// <param name="scene">The scene to render.</param>
    /// <param name="view">The camera view (matrices and position) to render with.</param>
    /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
    public void Render(Scene scene, in CameraView view, float deltaTime)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scene);

        if (!_initialized)
        {
            throw new InvalidOperationException("Renderer must be initialized before rendering.");
        }

        Matrix4x4 sceneAxis = GetSceneAxisMatrix(scene.UpAxis);

        _commandList.Reset();
        _commandList.SetSceneAxis(sceneAxis);
        _commandList.SetFrontFaceWinding(FaceWinding.CounterClockwise);
        _commandList.SetAmbientColor(AmbientColor);
        _commandList.SetUseViewBasedLighting(UseViewBasedLighting);
        _commandList.SetUseFlatShading(UseFlatShading);
        _commandList.SetSkinningMode(SkinningMode);
        _commandList.ResetLights(FallbackLightDirection, FallbackLightColor, FallbackLightIntensity);

        Vector2 viewportSize = new(_viewportWidth, _viewportHeight);
        IGpuRenderTarget? antiAliasingRenderTarget = EnsureAntiAliasingRenderTarget();
        RenderFrameContext context = CreateFrameContext(scene, view, viewportSize, deltaTime);
        context.Set<ICommandList>(_commandList);
        context.Set<IGraphicsDevice>(_graphicsDevice);
        if (antiAliasingRenderTarget is not null)
        {
            context.Set(antiAliasingRenderTarget);
        }


        IReadOnlyList<SceneNode> sceneTraversalNodes = GetSceneTraversalNodes(scene);
        _textureLoader.ApplyCompleted();
        _textureLoader.QueueLoads(sceneTraversalNodes, scene);

        if (_graphicsDevice is IFrameBufferUploadBudget uploadBudget)
        {
            uploadBudget.BeginFrameBufferUploads();
        }

        UpdateSceneRenderHandles(sceneTraversalNodes);
        UpdateSkybox(scene.Skybox, scene);
        UpdateGrid(scene.Grid);
        CollectVisibleHandles(view, sceneAxis);

        bool shadesSurfaces = !UseFlatShading && WireframeMode != WireframeMode.WireOnly;
        if (shadesSurfaces)
        {
            RenderShadowMaps(scene.Skybox, sceneAxis, view.Position, viewportSize);
        }

        if (shadesSurfaces && AmbientOcclusionQuality != AmbientOcclusionQuality.Off && _graphicsDevice.SupportsFormat(ImageFormat.D32Float, TextureUsage.DepthStencil | TextureUsage.Sampled))
        {
            _depthPrepass.Execute(context);
            _ambientOcclusionPass.Execute(context);
        }

        _clearAndStateResetPass.Execute(context);

        for (int i = 0; i < RenderPhases.Length; i++)
        {
            RenderFlags phase = RenderPhases[i];
            IReadOnlyList<MeshRenderHandle> meshes = phase switch
            {
                RenderFlags.Opaque		=> _opaqueMeshes,
                RenderFlags.Transparent	=> _transparentMeshes,
                _						=> [],
            };

            RenderSkybox(scene.Skybox, phase, view.ViewMatrix, view.ProjectionMatrix, view.Position, viewportSize);
            SceneTraversal.Render(meshes, _commandList, phase, view.ViewMatrix, view.ProjectionMatrix, sceneAxis, view.Position, viewportSize);

            if (phase == RenderFlags.Overlay)
            {
                RenderWireframes(view.ViewMatrix, view.ProjectionMatrix, sceneAxis, view.Position);
                RenderNormals(view.ViewMatrix, view.ProjectionMatrix, sceneAxis, view.Position);
            }

            SceneTraversal.Render(_otherHandles, _commandList, phase, view.ViewMatrix, view.ProjectionMatrix, sceneAxis, view.Position, viewportSize);
            RenderGrid(scene.Grid, phase, view.ViewMatrix, view.ProjectionMatrix, view.Position, viewportSize);
        }

        if (antiAliasingRenderTarget is not null)
        {
            _commandList.ResolveRenderTarget(antiAliasingRenderTarget, null);
        }

        _graphicsDevice.Submit(_commandList);
        HasPendingWork = HasPendingSceneWork(sceneTraversalNodes, scene.Skybox);
    }

    /// <summary>
    /// Releases renderer-owned resources attached to a scene without disposing the scene model.
    /// </summary>
    /// <param name="scene">The scene whose renderer resources should be released.</param>
    public void ReleaseResources(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _sceneTraversalScene = null;
        _sceneTraversalVersion = -1;
        _sceneTraversalNodes = [];
        _sceneTraversalHandles.Clear();
        ReleaseSkyboxResources(scene.Skybox);
        ReleaseGridResources(scene.Grid);
        ReleaseResources(scene.RootNode);
    }

    /// <summary>
    /// Releases renderer-owned resources attached to a scene-node subtree without disposing the nodes.
    /// </summary>
    /// <param name="node">The node subtree whose renderer resources should be released.</param>
    public void ReleaseResources(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        SceneRenderResources.Release(_graphicsDevice, node);

        if (node.Children is null)
        {
            return;
        }

        foreach (SceneNode child in node.Children)
        {
            ReleaseResources(child);
        }
    }

    /// <summary>
    /// Releases renderer resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _textureLoader.Dispose();
        ReleaseAntiAliasingResources();
        _clearAndStateResetPass.Dispose();
        _depthPrepass.Dispose();
        _ambientOcclusionPass.Dispose();
        SceneRenderResources.ReleaseAll(_graphicsDevice);
        ReleasePreparedPipelines();
        _initialized = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Collects the scene nodes in render traversal order, refreshing the cached list when the scene changes.</summary>
    /// <param name="scene">The scene whose nodes are collected.</param>
    /// <returns>The scene nodes in post-order traversal.</returns>
    private IReadOnlyList<SceneNode> GetSceneTraversalNodes(Scene scene)
    {
        if (ReferenceEquals(_sceneTraversalScene, scene) && _sceneTraversalVersion == scene.Version)
        {
            return _sceneTraversalNodes;
        }

        if (SceneTraversal.TryGetPreparedPostOrder(scene, out IReadOnlyList<SceneNode> preparedNodes))
        {
            _sceneTraversalNodes = preparedNodes;
        }
        else
        {
            List<SceneNode> nodes = [];
            SceneTraversal.CollectPostOrder(scene.RootNode, nodes);
            SceneNode[] collectedNodes = [.. nodes];
            SceneTraversal.CachePreparedPostOrder(scene, collectedNodes);
            _sceneTraversalNodes = collectedNodes;
        }

        _sceneTraversalScene = scene;
        _sceneTraversalVersion = scene.Version;
        return _sceneTraversalNodes;
    }

    private bool HasPendingSceneWork(IReadOnlyList<SceneNode> nodes, Skybox skybox)
    {
        if (_hasPendingUploads || _textureLoader.HasPendingLoads || ShowNormals && _visibleMeshes.Any(mesh => mesh.HasPendingNormalUploads))
        {
            return true;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is Texture { Data: null, LoadAttempted: false, ImageLoader: not null })
            {
                return true;
            }
        }

        return skybox is { Enabled: true, Texture: { } skyboxTexture } && SceneRenderResources.Get(_graphicsDevice, skyboxTexture)?.HasPendingUploads == true;
    }

    private void UpdateSceneRenderHandles(IReadOnlyList<SceneNode> nodes)
    {
        _sceneTraversalHandles.Clear();
        _hasPendingUploads = false;
        for (int i = 0; i < nodes.Count; i++)
        {
            SceneNode node = nodes[i];
            if ((node.Flags & SceneNodeFlags.NoDraw) != 0)
                continue;

            IRenderHandle? handle = SceneRenderResources.GetOrCreate(node, _graphicsDevice, _graphicsDevice.MaterialTypes);
            if (handle is null)
                continue;

            if (handle.RequiresPerFrameUpdate)
                handle.Update(_commandList);

            _hasPendingUploads |= handle.HasPendingUploads;
            if (!handle.Flags.HasFlag(RenderHandleFlags.SubHandle))
                _sceneTraversalHandles.Add((node, handle));
        }
    }

    private void CollectVisibleHandles(in CameraView view, in Matrix4x4 sceneAxis)
    {
        ViewFrustum frustum = ViewFrustum.FromViewProjection(view.ViewMatrix * view.ProjectionMatrix);
        _opaqueMeshes.Clear();
        _transparentMeshes.Clear();
        _visibleMeshes.Clear();
        _otherHandles.Clear();

        foreach ((SceneNode _, IRenderHandle handle) in _sceneTraversalHandles)
        {
            if (handle is not MeshRenderHandle meshHandle)
            {
                _otherHandles.Add(handle);
                continue;
            }

            if (!meshHandle.UpdateVisibility(frustum, sceneAxis, view.Position))
            {
                continue;
            }

            _visibleMeshes.Add(meshHandle);
            if (WireframeMode == WireframeMode.WireOnly)
            {
                continue;
            }

            if (meshHandle.HasOpaqueMaterials)
            {
                _opaqueMeshes.Add(meshHandle);
            }

            if (meshHandle.HasTransparentMaterials)
            {
                _transparentMeshes.Add(meshHandle);
            }
        }

        _opaqueMeshes.Sort(FrontToBack);
        _transparentMeshes.Sort(BackToFront);
    }

    private IGpuRenderTarget? EnsureAntiAliasingRenderTarget()
    {
        if (_externalAntiAliasingSamples > MinimumAntiAliasingSamples)
        {
            ReleaseAntiAliasingResources();
            _actualAntiAliasingSamples = _externalAntiAliasingSamples;
            return null;
        }

        if (_antiAliasingSamples <= MinimumAntiAliasingSamples)
        {
            ReleaseAntiAliasingResources();
            _actualAntiAliasingSamples = MinimumAntiAliasingSamples;
            return null;
        }

        int sampleCount = GetSupportedAntiAliasingSampleCount(_antiAliasingSamples);
        if (sampleCount <= MinimumAntiAliasingSamples)
        {
            ReleaseAntiAliasingResources();
            _actualAntiAliasingSamples = MinimumAntiAliasingSamples;
            return null;
        }

        if (_antiAliasingRenderTarget is not null && _antiAliasingTargetWidth == _viewportWidth && _antiAliasingTargetHeight == _viewportHeight && _actualAntiAliasingSamples == sampleCount)
        {
            return _antiAliasingRenderTarget;
        }

        ReleaseAntiAliasingResources();
        try
        {
            _antiAliasingColorTexture = _graphicsDevice.CreateTexture(_viewportWidth, _viewportHeight, AntiAliasingColorFormat, TextureUsage.RenderTarget, sampleCount);
            _antiAliasingDepthTexture = _graphicsDevice.CreateTexture(_viewportWidth, _viewportHeight, AntiAliasingDepthFormat, TextureUsage.DepthStencil, sampleCount);
            _antiAliasingRenderTarget = _graphicsDevice.CreateRenderTarget(_antiAliasingColorTexture, _antiAliasingDepthTexture);
        }
        catch (NotSupportedException)
        {
            ReleaseAntiAliasingResources();
            return null;
        }
        catch (InvalidOperationException)
        {
            ReleaseAntiAliasingResources();
            return null;
        }

        _antiAliasingTargetWidth = _viewportWidth;
        _antiAliasingTargetHeight = _viewportHeight;
        _actualAntiAliasingSamples = sampleCount;
        return _antiAliasingRenderTarget;
    }

    private int GetSupportedAntiAliasingSampleCount(int requestedSampleCount)
    {
        int colorSampleCount = _graphicsDevice.GetSupportedTextureSampleCount(AntiAliasingColorFormat, TextureUsage.RenderTarget, requestedSampleCount);
        int depthSampleCount = _graphicsDevice.GetSupportedTextureSampleCount(AntiAliasingDepthFormat, TextureUsage.DepthStencil, colorSampleCount);
        return Math.Min(colorSampleCount, depthSampleCount);
    }

    private RenderFrameContext CreateFrameContext(Scene scene, in CameraView view, Vector2 viewportSize, float deltaTime)
    {
        RenderFrameContext context = _frameContext ??= new(scene, view, viewportSize, deltaTime);
        context.ResetFrame(scene, view, viewportSize, deltaTime);
        context.AmbientColor = AmbientColor;
        context.FallbackLightDirection = FallbackLightDirection;
        context.FallbackLightColor = FallbackLightColor;
        context.FallbackLightIntensity = FallbackLightIntensity;
        context.UseViewBasedLighting = UseViewBasedLighting;
        context.SkinningMode = SkinningMode;
        return context;
    }

    private void UpdateGrid(Grid grid)
    {
        if (!grid.Enabled)
        {
            return;
        }

        SceneRenderResources.GetOrCreate(_graphicsDevice, grid, () => new GridRenderHandle(_graphicsDevice, _graphicsDevice.MaterialTypes, grid)).Update(_commandList);
    }

    private void UpdateSkybox(Skybox skybox, Scene scene)
    {
        if (!skybox.Enabled)
        {
            return;
        }

        SkyboxRenderHandle skyboxHandle = SceneRenderResources.GetOrCreate(_graphicsDevice, skybox, () => new SkyboxRenderHandle(_graphicsDevice, _graphicsDevice.MaterialTypes, skybox, scene));
        skyboxHandle.Update(_commandList);
        _commandList.SetEnvironmentMap(skyboxHandle.EnvironmentTexture, skyboxHandle.MipLevels, skybox.Intensity);

        foreach (EnvironmentLight light in skyboxHandle.Lights)
        {
            _commandList.AppendEnvironmentLight(light.Direction, light.Power * skybox.Intensity, light.HalfAngles);
        }
    }

    private void RenderShadowMaps(Skybox skybox, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        if (!skybox.Enabled || SceneRenderResources.Get(_graphicsDevice, skybox) is not SkyboxRenderHandle { Lights.Length: > 0 } skyboxHandle)
        {
            return;
        }

        int mapSize = ShadowQuality switch
        {
            ShadowQuality.UltraLow	=> 256,
            ShadowQuality.Low		=> 512,
            ShadowQuality.Medium	=> 1024,
            ShadowQuality.High		=> 2048,
            ShadowQuality.UltraHigh	=> 4096,
            _						=> 0,
        };

        int filterTaps = ShadowQuality switch
        {
            ShadowQuality.UltraLow	=> 2,
            ShadowQuality.Low		=> 3,
            ShadowQuality.Medium	=> 4,
            ShadowQuality.High		=> 6,
            ShadowQuality.UltraHigh	=> 8,
            _						=> 0,
        };

        if (mapSize == 0)
        {
            _shadowCount = 0;
            _commandList.EndShadowMaps(0, 0, 0.0f);
            return;
        }

        HashCode meshesKeyBuilder = new();
        HashCode keyBuilder = new();
        foreach ((SceneNode _, IRenderHandle handle) in _sceneTraversalHandles)
        {
            if (handle is MeshRenderHandle meshHandle)
            {
                meshesKeyBuilder.Add(meshHandle);
                meshesKeyBuilder.Add(meshHandle.VertexCount);
                meshesKeyBuilder.Add(meshHandle.HasPendingUploads);
                meshesKeyBuilder.Add(meshHandle.Owner.Positions);
                keyBuilder.Add(meshHandle.GeometryVersion);
                keyBuilder.Add(meshHandle.Owner.GetBindWorldMatrix());
            }
        }

        EnvironmentLight[] lights = skyboxHandle.Lights;
        int meshesKey = meshesKeyBuilder.ToHashCode();
        int key = keyBuilder.ToHashCode();

        bool redrawAll = !ReferenceEquals(lights, _shadowLights) || sceneAxis != _shadowSceneAxis || _shadowCount == 0 || mapSize != _shadowMapSize || ShadowSoftness != _shadowSoftness;
        if (!redrawAll && (key != _shadowKey || meshesKey != _shadowMeshesKey))
        {
            _staleShadowCount = _shadowCount;
        }

        if (redrawAll || meshesKey != _shadowMeshesKey)
        {
            _shadowBounds = SceneBounds.Invalid;
            foreach ((SceneNode _, IRenderHandle handle) in _sceneTraversalHandles)
            {
                if (handle is MeshRenderHandle { VertexCount: > 0 } meshHandle)
                {
                    _shadowBounds = _shadowBounds.Include(meshHandle.GetWorldBounds(sceneAxis));
                }
            }

            _shadowBounds = _shadowBounds.Expand(_shadowBounds.Radius * 0.1f);
        }

        _shadowKey = key;
        _shadowMeshesKey = meshesKey;

        SceneBounds bounds = _shadowBounds;
        if (!bounds.IsValid || bounds.Radius <= 0.0f || (!redrawAll && _staleShadowCount == 0))
        {
            _commandList.EndShadowMaps(_shadowCount, filterTaps, ShadowStrength);
            return;
        }

        if (redrawAll)
        {
            _shadowLights = lights;
            _shadowSceneAxis = sceneAxis;
            _shadowMapSize = mapSize;
            _shadowSoftness = ShadowSoftness;
            _shadowCount = 0;
            _staleShadowCount = 0;

            while (_shadowCount < lights.Length && RenderShadowMap(lights[_shadowCount], _shadowCount, mapSize, bounds, sceneAxis, cameraPosition, viewportSize))
            {
                _shadowCount++;
            }
        }
        else
        {
            _nextShadowIndex = (_nextShadowIndex + 1) % _shadowCount;
            RenderShadowMap(lights[_nextShadowIndex], _nextShadowIndex, mapSize, bounds, sceneAxis, cameraPosition, viewportSize);
            _staleShadowCount--;
        }

        _commandList.EndShadowMaps(_shadowCount, filterTaps, ShadowStrength);
    }

    private bool RenderShadowMap(EnvironmentLight light, int index, int mapSize, SceneBounds bounds, in Matrix4x4 sceneAxis, Vector3 cameraPosition, Vector2 viewportSize)
    {
        Matrix4x4	view			= Matrix4x4.CreateLookAt(bounds.Center, bounds.Center - light.Direction, Vector3.Cross(light.Direction, light.MajorAxis));
        SceneBounds	lightBounds		= bounds.Transform(view).Expand(bounds.Radius * 0.02f);
        Vector3		size			= lightBounds.Size;
        Matrix4x4	projection		= Matrix4x4.CreateOrthographicOffCenter(lightBounds.Min.X, lightBounds.Max.X, lightBounds.Min.Y, lightBounds.Max.Y, -lightBounds.Max.Z, -lightBounds.Min.Z);
        Matrix4x4	shadowMatrix	= view * projection * Matrix4x4.CreateScale(0.5f, 0.5f, 0.5f) * Matrix4x4.CreateTranslation(0.5f, 0.5f, 0.5f);
        Vector2		filterRadius	= new Vector2(1.0f / size.X, 1.0f / size.Y) * (bounds.Radius * ShadowBlurRadius * ShadowSoftness);

        if (!_commandList.BeginShadowMap(index, mapSize, shadowMatrix, filterRadius))
        {
            return false;
        }

        SceneTraversal.Render(_sceneTraversalHandles, _commandList, RenderFlags.Shadow, view, projection, sceneAxis, cameraPosition, viewportSize);
        return true;
    }

    private void RenderWireframes(in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition)
    {
        if (WireframeMode == WireframeMode.None || GetWireframePipeline() is not { } pipeline)
        {
            return;
        }

        foreach (MeshRenderHandle meshHandle in _visibleMeshes)
        {
            meshHandle.RenderWireframe(_commandList, pipeline, WireframeDepthBias, view, projection, sceneAxis, cameraPosition);
        }
    }

    private void RenderNormals(in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 sceneAxis, Vector3 cameraPosition)
    {
        if (!ShowNormals || _graphicsDevice.MaterialTypes is not IMaterialPipelineProvider pipelineProvider)
        {
            return;
        }

        _normalPipeline ??= pipelineProvider.AcquirePipeline(_graphicsDevice, "Normals");
        foreach (MeshRenderHandle meshHandle in _visibleMeshes)
        {
            float length = MathF.Max(meshHandle.GetWorldBounds(sceneAxis).Radius * 0.03f, 0.001f);
            meshHandle.RenderNormals(_commandList, _normalPipeline, length, view, projection, sceneAxis, cameraPosition);
        }
    }

    private IGpuPipelineState? GetWireframePipeline()
    {
        if (_wireframePipeline is { IsDisposed: false })
        {
            return _wireframePipeline;
        }

        if (_graphicsDevice.MaterialTypes is not IMaterialPipelineProvider pipelineProvider || !_graphicsDevice.MaterialTypes.Contains(WireframeMaterialTypeName))
        {
            return null;
        }

        _wireframePipeline = pipelineProvider.AcquirePipeline(_graphicsDevice, WireframeMaterialTypeName);
        return _wireframePipeline;
    }

    private void RenderSkybox(Skybox skybox, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, Vector3 cameraPosition, Vector2 viewportSize)
    {
        IRenderHandle? graphicsHandle = SceneRenderResources.Get(_graphicsDevice, skybox);
        if (!skybox.Enabled || graphicsHandle is null)
        {
            return;
        }

        graphicsHandle.Render(_commandList, phase, view, projection, Matrix4x4.Identity, cameraPosition, viewportSize);
    }

    private void RenderGrid(Grid grid, RenderFlags phase, in Matrix4x4 view, in Matrix4x4 projection, Vector3 cameraPosition, Vector2 viewportSize)
    {
        IRenderHandle? graphicsHandle = SceneRenderResources.Get(_graphicsDevice, grid);
        if (!grid.Enabled || graphicsHandle is null)
        {
            return;
        }

        graphicsHandle.Render(_commandList, phase, view, projection, Matrix4x4.Identity, cameraPosition, viewportSize);
    }

    private void ReleaseGridResources(Grid grid)
    {
        SceneRenderResources.Release(_graphicsDevice, grid);
    }

    private void ReleaseSkyboxResources(Skybox skybox)
    {
        SceneRenderResources.Release(_graphicsDevice, skybox);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ReleasePreparedPipelines()
    {
        if (_graphicsDevice.MaterialTypes is IMaterialPipelineProvider pipelineProvider)
        {
            foreach (KeyValuePair<string, IGpuPipelineState> prepared in _preparedPipelines)
            {
                pipelineProvider.ReleasePipeline(prepared.Key, prepared.Value);
            }

            if (_normalPipeline is not null)
            {
                pipelineProvider.ReleasePipeline("Normals", _normalPipeline);
            }
            if (_wireframePipeline is not null)
            {
                pipelineProvider.ReleasePipeline(WireframeMaterialTypeName, _wireframePipeline);
            }
        }

        _preparedPipelines.Clear();
        _wireframePipeline	= null;
        _normalPipeline		= null;
    }

    private void ReleaseAntiAliasingResources()
    {
        _antiAliasingRenderTarget?.Dispose();
        _antiAliasingRenderTarget = null;
        _antiAliasingDepthTexture?.Dispose();
        _antiAliasingDepthTexture = null;
        _antiAliasingColorTexture?.Dispose();
        _antiAliasingColorTexture = null;
        _antiAliasingTargetWidth = 0;
        _antiAliasingTargetHeight = 0;
        _actualAntiAliasingSamples = MinimumAntiAliasingSamples;
    }

    internal static Matrix4x4 GetSceneAxisMatrix(SceneUpAxis upAxis)
    {
        return upAxis switch
        {
            SceneUpAxis.X => Matrix4x4.CreateRotationZ(MathF.PI / 2.0f),
            SceneUpAxis.Z => Matrix4x4.CreateRotationX(-MathF.PI / 2.0f),
            _ => Matrix4x4.Identity,
        };
    }
}
