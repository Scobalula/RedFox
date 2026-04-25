using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Backend;
using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Provides a backend-driven scene renderer that draws a scene with a camera.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    private static readonly RenderPhase[] RenderPhases =
    [
        RenderPhase.SkinningCompute,
        RenderPhase.Opaque,
        RenderPhase.Transparent,
        RenderPhase.Overlay,
    ];

    private readonly ClearAndStateResetPass _clearAndStateResetPass;
    private readonly ICommandList _commandList;
    private readonly IGraphicsDevice _graphicsDevice;

    private bool _disposed;
    private bool _initialized;
    private int _viewportHeight = 1;
    private int _viewportWidth = 1;

    /// <summary>
    /// Gets the graphics device used by the renderer.
    /// </summary>
    public IGraphicsDevice GraphicsDevice => _graphicsDevice;

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
        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
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
        RenderFrameContext context = CreateFrameContext(scene, view, viewportSize, deltaTime);
        context.Set<ICommandList>(_commandList);
        context.Set<IGraphicsDevice>(_graphicsDevice);

        _clearAndStateResetPass.Execute(context);
        SceneTraversal.Update(scene.RootNode, _commandList, _graphicsDevice, _graphicsDevice.MaterialTypes);

        for (int i = 0; i < RenderPhases.Length; i++)
        {
            SceneTraversal.Render(
                scene.RootNode,
                _commandList,
                RenderPhases[i],
                view.ViewMatrix,
                view.ProjectionMatrix,
                sceneAxis,
                view.Position,
                viewportSize);
        }

        _graphicsDevice.Submit(_commandList);
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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