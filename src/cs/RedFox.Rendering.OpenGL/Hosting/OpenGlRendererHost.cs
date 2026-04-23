using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System;

namespace RedFox.Rendering.OpenGL.Hosting;

/// <summary>
/// Minimal windowing host: creates the GL window, owns the <see cref="OpenGlSceneRenderer"/>,
/// surfaces the input context, and forwards a per-frame callback. The host does not own
/// scenes or cameras — callers supply those when invoking <c>Renderer.Render</c> from the
/// frame callback.
/// </summary>
public sealed class OpenGlRendererHost : IDisposable
{
    private readonly IWindow _window;
    private readonly OpenGlSceneRenderer _renderer;
    private IInputContext? _inputContext;
    private Action<double, IInputContext, OpenGlSceneRenderer>? _frameCallback;
    private bool _rendererDisposedOnClosing;
    private bool _disposed;

    /// <summary>
    /// Initializes a new windowed OpenGL renderer host.
    /// </summary>
    /// <param name="title">The window title.</param>
    /// <param name="width">The initial window width in pixels.</param>
    /// <param name="height">The initial window height in pixels.</param>
    /// <param name="settings">The renderer settings.</param>
    public OpenGlRendererHost(string title, int width, int height, OpenGlRenderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        WindowOptions options = WindowOptions.Default;
        options.Title = title;
        options.Size = new Vector2D<int>(width, height);
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(4, 3));
        _window = Silk.NET.Windowing.Window.Create(options);
        _renderer = new OpenGlSceneRenderer(_window, settings);

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
    }

    /// <summary>
    /// Gets the active <see cref="OpenGlSceneRenderer"/>. Useful for adding custom passes
    /// before <see cref="Run(Action{double, IInputContext, OpenGlSceneRenderer})"/>.
    /// </summary>
    public OpenGlSceneRenderer Renderer => _renderer;

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
    /// for invoking <see cref="OpenGlSceneRenderer.Render(Graphics3D.Scene, in Graphics3D.CameraView, float)"/>.
    /// </summary>
    /// <param name="frameCallback">Per-frame callback.</param>
    public void Run(Action<double, IInputContext, OpenGlSceneRenderer> frameCallback)
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
        if (!_rendererDisposedOnClosing)
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
        _renderer.Initialize();
        Vector2D<int> framebufferSize = _window.FramebufferSize;
        _renderer.Resize(framebufferSize.X, framebufferSize.Y);
    }

    private void OnRender(double deltaTime)
    {
        if (_frameCallback is null || _inputContext is null)
        {
            return;
        }

        _frameCallback(deltaTime, _inputContext, _renderer);
    }

    private void OnFramebufferResize(Vector2D<int> framebufferSize)
    {
        _renderer.Resize(framebufferSize.X, framebufferSize.Y);
    }

    private void OnClosing()
    {
        if (_disposed || _rendererDisposedOnClosing)
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
}
