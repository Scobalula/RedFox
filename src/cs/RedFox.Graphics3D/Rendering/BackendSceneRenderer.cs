using RedFox.Graphics3D.Rendering.Backend;
using System;
using System.Numerics;

namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Provides a backend-driven scene renderer that executes the shared traversal path against an <see cref="IGraphicsDevice"/>.
/// </summary>
public sealed class BackendSceneRenderer : SceneRenderer
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
    private Matrix4x4 _sceneAxis = Matrix4x4.Identity;
    private int _viewportHeight = 1;
    private int _viewportWidth = 1;

    /// <summary>
    /// Gets the graphics device used by the renderer.
    /// </summary>
    public IGraphicsDevice GraphicsDevice => _graphicsDevice;

    /// <summary>
    /// Gets or sets the scene-axis transform applied during rendering.
    /// </summary>
    public Matrix4x4 SceneAxis
    {
        get => _sceneAxis;
        set => _sceneAxis = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendSceneRenderer"/> class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device that executes rendering work.</param>
    /// <param name="clearColor">The clear color applied at frame start.</param>
    /// <param name="ambientColor">The ambient light contribution.</param>
    /// <param name="fallbackLightDirection">The fallback light direction.</param>
    /// <param name="fallbackLightColor">The fallback light color.</param>
    /// <param name="fallbackLightIntensity">The fallback light intensity.</param>
    /// <param name="useViewBasedLighting">Whether view-based lighting is enabled.</param>
    /// <param name="skinningMode">The skinning mode used during rendering.</param>
    public BackendSceneRenderer(
        IGraphicsDevice graphicsDevice,
        Vector4 clearColor,
        Vector3 ambientColor,
        Vector3 fallbackLightDirection,
        Vector3 fallbackLightColor,
        float fallbackLightIntensity,
        bool useViewBasedLighting,
        SkinningMode skinningMode)
        : base(ambientColor, fallbackLightDirection, fallbackLightColor, fallbackLightIntensity, useViewBasedLighting, skinningMode)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _commandList = graphicsDevice.CreateCommandList();
        _clearAndStateResetPass = new ClearAndStateResetPass(clearColor);
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        ThrowIfDisposed();
        if (_initialized)
        {
            return;
        }

        _clearAndStateResetPass.Initialize();
        _initialized = true;
    }

    /// <inheritdoc/>
    public override void Resize(int width, int height)
    {
        ThrowIfDisposed();
        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
        _clearAndStateResetPass.Resize(_viewportWidth, _viewportHeight);
    }

    /// <inheritdoc/>
    public override void Render(Scene scene, in CameraView view, float deltaTime)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scene);

        if (!_initialized)
        {
            throw new InvalidOperationException("Renderer must be initialized before rendering.");
        }

        _commandList.Reset();

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
                _sceneAxis,
                view.Position,
                viewportSize);
        }

        _graphicsDevice.Submit(_commandList);
    }

    /// <inheritdoc/>
    public override void Dispose()
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}