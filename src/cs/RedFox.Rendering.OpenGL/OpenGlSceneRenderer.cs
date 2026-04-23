using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL.Resources;
using RedFox.Rendering.OpenGL.Passes;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;

namespace RedFox.Rendering.OpenGL;

/// <summary>
/// Orchestrates the default OpenGL render pipeline. Owns the <see cref="OpenGlContext"/>,
/// shared GPU resources, and a mutable <see cref="IRenderPipeline"/> the caller can extend
/// with custom passes (SSAO, shadows, post-process, etc.).
/// </summary>
public sealed class OpenGlSceneRenderer : SceneRenderer
{
    private const int RequiredOpenGlMajorVersion = 4;
    private const int RequiredOpenGlMinorVersion = 3;

    private readonly IWindow _window;
    private readonly OpenGlRenderSettings _settings;
    private readonly RenderPipeline _pipeline = new();

    private OpenGlContext? _context;
    private OpenGlRenderResources? _resources;
    private OpenGlSceneCollectionPass? _collectionPass;
    private int _viewportWidth;
    private int _viewportHeight;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="OpenGlSceneRenderer"/> bound to the supplied window's GL context.
    /// </summary>
    /// <param name="window">The host window owning the GL context.</param>
    /// <param name="settings">The render settings.</param>
    public OpenGlSceneRenderer(IWindow window, OpenGlRenderSettings settings)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Gets the mutable render pipeline. Callers may add or remove passes between frames.
    /// </summary>
    public IRenderPipeline Pipeline => _pipeline;

    /// <summary>
    /// Gets the active OpenGL context wrapper. Available after <see cref="Initialize"/>.
    /// </summary>
    public OpenGlContext Context => _context ?? throw new InvalidOperationException("Renderer must be initialized before accessing the context.");

    /// <summary>
    /// Gets the active render settings.
    /// </summary>
    public OpenGlRenderSettings Settings => _settings;

    /// <inheritdoc/>
    public override void Initialize()
    {
        ThrowIfDisposed();
        if (_initialized)
        {
            return;
        }

        GL gl = GL.GetApi(_window);
        _context = new OpenGlContext(gl);
        _context.RequireVersion(RequiredOpenGlMajorVersion, RequiredOpenGlMinorVersion);
        Console.WriteLine($"[OpenGL] Context: {_context.MajorVersion}.{_context.MinorVersion} (requires {RequiredOpenGlMajorVersion}.{RequiredOpenGlMinorVersion}+). SkinningMode: {_settings.SkinningMode}");

        _resources = new OpenGlRenderResources(_context, _settings);

        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Back);
        _context.SetFrontFace(_settings.FaceWinding == OpenGlFaceWinding.Ccw);

        _collectionPass = new OpenGlSceneCollectionPass(_resources);
        _pipeline.Add(new OpenGlClearAndStateResetPass(_resources));
        _pipeline.Add(_collectionPass);
        _pipeline.Add(new OpenGlSkinningComputePass(_resources));
        _pipeline.Add(new OpenGlOpaqueGeometryPass(_resources));
        _pipeline.Add(new OpenGlTransparentGeometryPass(_resources));
        _pipeline.Add(new OpenGlSkeletonOverlayPass(_resources));

        _pipeline.Initialize();
        _initialized = true;
    }

    /// <inheritdoc/>
    public override void Resize(int width, int height)
    {
        ThrowIfDisposed();
        if (!_initialized || _context is null)
        {
            return;
        }

        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
        _context.SetViewport(_viewportWidth, _viewportHeight);
        _pipeline.Resize(_viewportWidth, _viewportHeight);
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

        System.Numerics.Vector2 viewportSize = new(Math.Max(1, _viewportWidth), Math.Max(1, _viewportHeight));
        RenderFrameContext context = new(scene, view, viewportSize, deltaTime);
        _pipeline.Execute(context);
    }

    /// <summary>
    /// Releases cached per-node GPU handles so they're rebuilt on the next frame.
    /// </summary>
    public void RebuildResources()
    {
        ThrowIfDisposed();
        _collectionPass?.ReleaseTrackedHandles();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _collectionPass?.DisposeTrackedHandles();
        _pipeline.Dispose();
        _resources?.Dispose();
        _context?.Dispose();
        _resources = null;
        _context = null;
        _collectionPass = null;
        _initialized = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
