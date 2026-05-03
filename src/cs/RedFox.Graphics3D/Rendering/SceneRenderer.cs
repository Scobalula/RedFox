using System;
using System.Numerics;
using RedFox.Graphics2D;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Handles;

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

    private static readonly RenderPhase[] RenderPhases =
    [
        RenderPhase.Opaque,
        RenderPhase.Transparent,
        RenderPhase.Overlay,
    ];

    private readonly ClearAndStateResetPass _clearAndStateResetPass;
    private readonly ICommandList _commandList;
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly List<SceneNode> _sceneTraversalNodes = [];

    private IGpuTexture? _antiAliasingColorTexture;
    private IGpuTexture? _antiAliasingDepthTexture;
    private IGpuRenderTarget? _antiAliasingRenderTarget;
    private int _actualAntiAliasingSamples = MinimumAntiAliasingSamples;
    private int _antiAliasingSamples = DefaultAntiAliasingSamples;
    private int _antiAliasingTargetHeight;
    private int _antiAliasingTargetWidth;
    private int _externalAntiAliasingSamples = MinimumAntiAliasingSamples;
    private bool _disposed;
    private bool _initialized;
    private Scene? _sceneTraversalScene;
    private long _sceneTraversalVersion = -1;
    private int _viewportHeight = 1;
    private int _viewportWidth = 1;

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
    /// Gets or sets the skinning mode used during rendering.
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
    public SceneRenderer(
        IGraphicsDevice graphicsDevice,
        Vector4 clearColor,
        Vector3 ambientColor,
        Vector3 fallbackLightDirection,
        Vector3 fallbackLightColor,
        float fallbackLightIntensity,
        bool useViewBasedLighting,
        SkinningMode skinningMode)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _commandList = graphicsDevice.CreateCommandList();
        _clearAndStateResetPass = new ClearAndStateResetPass(clearColor);
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
        _commandList.SetFrontFaceWinding(scene.FaceWinding);
        _commandList.SetAmbientColor(AmbientColor);
        _commandList.SetUseViewBasedLighting(UseViewBasedLighting);
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

        _clearAndStateResetPass.Execute(context);
        SceneTraversal.Update(sceneTraversalNodes, _commandList, _graphicsDevice, _graphicsDevice.MaterialTypes);
        UpdateSkybox(scene.Skybox, scene);
        UpdateGrid(scene.Grid);

        for (int i = 0; i < RenderPhases.Length; i++)
        {
            RenderPhase phase = RenderPhases[i];
            RenderSkybox(scene.Skybox, phase, view.ViewMatrix, view.ProjectionMatrix, view.Position, viewportSize);
            SceneTraversal.Render(sceneTraversalNodes, _commandList, phase, view.ViewMatrix, view.ProjectionMatrix, sceneAxis, view.Position, viewportSize);
            RenderGrid(scene.Grid, phase, view.ViewMatrix, view.ProjectionMatrix, view.Position, viewportSize);
        }

        if (antiAliasingRenderTarget is not null)
        {
            _commandList.ResolveRenderTarget(antiAliasingRenderTarget, null);
        }

        _graphicsDevice.Submit(_commandList);
    }

    private IReadOnlyList<SceneNode> GetSceneTraversalNodes(Scene scene)
    {
        if (ReferenceEquals(_sceneTraversalScene, scene) && _sceneTraversalVersion == scene.Version)
        {
            return _sceneTraversalNodes;
        }

        _sceneTraversalNodes.Clear();
        SceneTraversal.CollectPostOrder(scene.RootNode, _sceneTraversalNodes);
        _sceneTraversalScene = scene;
        _sceneTraversalVersion = scene.Version;
        return _sceneTraversalNodes;
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

        if (_antiAliasingRenderTarget is not null
            && _antiAliasingTargetWidth == _viewportWidth
            && _antiAliasingTargetHeight == _viewportHeight
            && _actualAntiAliasingSamples == sampleCount)
        {
            return _antiAliasingRenderTarget;
        }

        ReleaseAntiAliasingResources();
        try
        {
            _antiAliasingColorTexture = _graphicsDevice.CreateTexture(
                _viewportWidth,
                _viewportHeight,
                AntiAliasingColorFormat,
                TextureUsage.RenderTarget,
                sampleCount);
            _antiAliasingDepthTexture = _graphicsDevice.CreateTexture(
                _viewportWidth,
                _viewportHeight,
                AntiAliasingDepthFormat,
                TextureUsage.DepthStencil,
                sampleCount);
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
        int colorSampleCount = _graphicsDevice.GetSupportedTextureSampleCount(
            AntiAliasingColorFormat,
            TextureUsage.RenderTarget,
            requestedSampleCount);
        int depthSampleCount = _graphicsDevice.GetSupportedTextureSampleCount(
            AntiAliasingDepthFormat,
            TextureUsage.DepthStencil,
            colorSampleCount);
        return Math.Min(colorSampleCount, depthSampleCount);
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
        _sceneTraversalNodes.Clear();
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

        if (node.GraphicsHandle is { } graphicsHandle)
        {
            graphicsHandle.Release();
            graphicsHandle.Dispose();
            node.GraphicsHandle = null;
        }

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

        ReleaseAntiAliasingResources();
        _clearAndStateResetPass.Dispose();
        _graphicsDevice.Dispose();
        _initialized = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a frame context populated with the renderer's per-frame lighting and skinning configuration.
    /// </summary>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="view">The active camera view.</param>
    /// <param name="viewportSize">The active viewport size.</param>
    /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
    /// <returns>The populated frame context.</returns>
    private RenderFrameContext CreateFrameContext(Scene scene, in CameraView view, Vector2 viewportSize, float deltaTime)
    {
        RenderFrameContext context = new(scene, view, viewportSize, deltaTime)
        {
            AmbientColor = AmbientColor,
            FallbackLightDirection = FallbackLightDirection,
            FallbackLightColor = FallbackLightColor,
            FallbackLightIntensity = FallbackLightIntensity,
            UseViewBasedLighting = UseViewBasedLighting,
            SkinningMode = SkinningMode,
        };

        return context;
    }

    private void UpdateGrid(Grid grid)
    {
        if (!grid.Enabled)
        {
            return;
        }

        grid.GraphicsHandle ??= new GridRenderHandle(_graphicsDevice, _graphicsDevice.MaterialTypes, grid);
        grid.GraphicsHandle.Update(_commandList);
    }

    private void UpdateSkybox(Skybox skybox, Scene scene)
    {
        if (!skybox.Enabled)
        {
            return;
        }

        skybox.GraphicsHandle ??= new SkyboxRenderHandle(_graphicsDevice, _graphicsDevice.MaterialTypes, skybox, scene);
        skybox.GraphicsHandle.Update(_commandList);
    }

    private void RenderSkybox(
        Skybox skybox,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        if (!skybox.Enabled || skybox.GraphicsHandle is null)
        {
            return;
        }

        skybox.GraphicsHandle.Render(
            _commandList,
            phase,
            view,
            projection,
            Matrix4x4.Identity,
            cameraPosition,
            viewportSize);
    }

    private void RenderGrid(
        Grid grid,
        RenderPhase phase,
        in Matrix4x4 view,
        in Matrix4x4 projection,
        Vector3 cameraPosition,
        Vector2 viewportSize)
    {
        if (!grid.Enabled || grid.GraphicsHandle is null)
        {
            return;
        }

        grid.GraphicsHandle.Render(
            _commandList,
            phase,
            view,
            projection,
            Matrix4x4.Identity,
            cameraPosition,
            viewportSize);
    }

    private static void ReleaseGridResources(Grid grid)
    {
        if (grid.GraphicsHandle is not { } graphicsHandle)
        {
            return;
        }

        graphicsHandle.Release();
        graphicsHandle.Dispose();
        grid.GraphicsHandle = null;
    }

    private static void ReleaseSkyboxResources(Skybox skybox)
    {
        if (skybox.GraphicsHandle is not { } graphicsHandle)
        {
            return;
        }

        graphicsHandle.Release();
        graphicsHandle.Dispose();
        skybox.GraphicsHandle = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

    private static Matrix4x4 GetSceneAxisMatrix(SceneUpAxis upAxis)
    {
        return upAxis switch
        {
            SceneUpAxis.X => Matrix4x4.CreateRotationZ(MathF.PI / 2.0f),
            SceneUpAxis.Z => Matrix4x4.CreateRotationX(-MathF.PI / 2.0f),
            _ => Matrix4x4.Identity,
        };
    }
}