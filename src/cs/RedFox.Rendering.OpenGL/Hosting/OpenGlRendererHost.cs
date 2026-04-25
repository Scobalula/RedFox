using RedFox.Graphics3D;
using RedFox.Graphics3D.OpenGL;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Hosting;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Numerics;

namespace RedFox.Rendering.OpenGL.Hosting;

/// <summary>
/// Minimal windowing host: creates the GL window, owns a backend-driven <see cref="SceneRenderer"/>,
/// surfaces the input context, and forwards a per-frame callback. The host does not own
/// scenes or cameras — callers supply those when invoking <c>Renderer.Render</c> from the
/// frame callback.
/// </summary>
public sealed class OpenGlRendererHost : IRendererHost
{
    private const int RequiredOpenGlMajorVersion = 4;
    private const int RequiredOpenGlMinorVersion = 3;

    private readonly Vector3 _ambientColor;
    private readonly Vector4 _clearColor;
    private readonly Vector3 _fallbackLightColor;
    private readonly Vector3 _fallbackLightDirection;
    private readonly float _fallbackLightIntensity;
    private readonly SkinningMode _skinningMode;
    private readonly bool _useViewBasedLighting;
    private readonly IWindow _window;
    private BackendSceneRenderer? _renderer;
    private IInputContext? _inputContext;
    private Action<double, IInputContext, SceneRenderer>? _frameCallback;
    private bool _rendererDisposedOnClosing;
    private bool _disposed;

    /// <summary>
    /// Initializes a new windowed OpenGL renderer host.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <param name="width">The initial window width in pixels.</param>
    /// <param name="height">The initial window height in pixels.</param>
    /// <param name="clearColor">The framebuffer clear color.</param>
    /// <param name="ambientColor">The ambient light contribution.</param>
    /// <param name="fallbackLightDirection">The fallback light direction.</param>
    /// <param name="fallbackLightColor">The fallback light color.</param>
    /// <param name="fallbackLightIntensity">The fallback light intensity.</param>
    /// <param name="useViewBasedLighting">Whether view-based lighting is enabled.</param>
    /// <param name="skinningMode">The skinning mode used during rendering.</param>
    public OpenGlRendererHost(
        string title,
        int width,
        int height,
        Vector4 clearColor,
        Vector3 ambientColor,
        Vector3 fallbackLightDirection,
        Vector3 fallbackLightColor,
        float fallbackLightIntensity,
        bool useViewBasedLighting,
        SkinningMode skinningMode)
    {
        _clearColor = clearColor;
        _ambientColor = ambientColor;
        _fallbackLightDirection = fallbackLightDirection;
        _fallbackLightColor = fallbackLightColor;
        _fallbackLightIntensity = fallbackLightIntensity;
        _useViewBasedLighting = useViewBasedLighting;
        _skinningMode = skinningMode;

        WindowOptions options = WindowOptions.Default;
        options.Title = title;
        options.Size = new Vector2D<int>(width, height);
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(4, 3));
        _window = Silk.NET.Windowing.Window.Create(options);

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
    }

    /// <summary>
    /// Gets the active backend-driven scene renderer.
    /// </summary>
    public SceneRenderer Renderer => _renderer ?? throw new InvalidOperationException("Renderer is not available before the window load event.");

    SceneRenderer IRendererHost.Renderer => Renderer;

    /// <summary>
    /// Gets the underlying Silk window so callers can subscribe to window-level events
    /// (framebuffer resize, close, etc.).
    /// </summary>
    public IWindow Window => _window;

    /// <summary>
    /// Gets the active Silk input context. Throws if accessed before the window has loaded.
    /// </summary>
    public IInputContext InputContext => _inputContext ?? throw new InvalidOperationException("Input context is not available before the window load event.");

    /// <summary>
    /// Runs the host render loop without a per-frame callback. The renderer is invoked
    /// each frame with no scene; intended for empty-window scenarios.
    /// </summary>
    public void Run()
    {
        Run(static (_, _, _) => { });
    }

    /// <summary>
    /// Runs the host render loop and invokes <paramref name="frameCallback"/> each frame.
    /// The callback receives the delta time, input context, and renderer; it is responsible
    /// for invoking <see cref="SceneRenderer.Render(Graphics3D.Scene, in Graphics3D.CameraView, float)"/>.
    /// </summary>
    /// <param name="frameCallback">Per-frame callback.</param>
    public void Run(Action<double, IInputContext, SceneRenderer> frameCallback)
    {
        ThrowIfDisposed();
        _frameCallback = frameCallback ?? throw new ArgumentNullException(nameof(frameCallback));
        try
        {
            _window.Run();
        }
        finally
        {
            _frameCallback = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Load -= OnLoad;
        _window.Render -= OnRender;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Closing -= OnClosing;
        _inputContext?.Dispose();
        _inputContext = null;
        if (!_rendererDisposedOnClosing && _renderer is not null)
        {
            _renderer.Dispose();
            _rendererDisposedOnClosing = true;
        }

        _window.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnLoad()
    {
        _inputContext = _window.CreateInput();
        GL gl = GL.GetApi(_window);
        ValidateOpenGlVersion(gl);

        Console.WriteLine($"[OpenGL] Context: {RequiredOpenGlMajorVersion}.{RequiredOpenGlMinorVersion}+ ready. SkinningMode: {_skinningMode}");

        OpenGlGraphicsDevice graphicsDevice = new(gl);
        _renderer = new BackendSceneRenderer(
            graphicsDevice,
            _clearColor,
            _ambientColor,
            _fallbackLightDirection,
            _fallbackLightColor,
            _fallbackLightIntensity,
            _useViewBasedLighting,
            _skinningMode);
        _renderer.Initialize();
        Vector2D<int> framebufferSize = _window.FramebufferSize;
        _renderer.Resize(framebufferSize.X, framebufferSize.Y);
    }

    private void OnRender(double deltaTime)
    {
        if (_frameCallback is null || _inputContext is null || _renderer is null)
        {
            return;
        }

        _frameCallback(deltaTime, _inputContext, _renderer);
    }

    private void OnFramebufferResize(Vector2D<int> framebufferSize)
    {
        _renderer?.Resize(framebufferSize.X, framebufferSize.Y);
    }

    private void OnClosing()
    {
        if (_disposed || _rendererDisposedOnClosing || _renderer is null)
        {
            return;
        }

        _renderer.Dispose();
        _rendererDisposedOnClosing = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void ValidateOpenGlVersion(GL gl)
    {
        int majorVersion = 0;
        int minorVersion = 0;
        gl.GetInteger(GLEnum.MajorVersion, out majorVersion);
        gl.GetInteger(GLEnum.MinorVersion, out minorVersion);
        if (majorVersion < RequiredOpenGlMajorVersion || (majorVersion == RequiredOpenGlMajorVersion && minorVersion < RequiredOpenGlMinorVersion))
        {
            throw new InvalidOperationException($"OpenGL {RequiredOpenGlMajorVersion}.{RequiredOpenGlMinorVersion}+ is required, but the active context is {majorVersion}.{minorVersion}.");
        }
    }
}
